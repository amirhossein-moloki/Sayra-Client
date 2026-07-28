using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.UpdatePlatform.Application.Dtos;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Scheduler that manages, executes, and orchestrates periodic update checks, downloads, and installations.
    /// </summary>
    public class UpdateScheduler : IUpdateScheduler, IDisposable
    {
        private readonly IUpdateManager _updateManager;
        private readonly IEligibilityEvaluator _eligibilityEvaluator;
        private readonly SchedulerOptions _schedulerOptions;
        private readonly DeploymentOptions _deploymentOptions;
        private readonly ILogger<UpdateScheduler> _logger;

        private readonly ConcurrentDictionary<Guid, ScheduledUpdateTask> _tasks = new();
        private readonly SemaphoreSlim _executionLock = new(1, 1);
        private CancellationTokenSource? _cts;
        private Task? _schedulerLoopTask;
        private bool _isStarted;

        public UpdateScheduler(
            IUpdateManager updateManager,
            IEligibilityEvaluator eligibilityEvaluator,
            IOptions<SchedulerOptions> schedulerOptions,
            IOptions<DeploymentOptions> deploymentOptions,
            ILogger<UpdateScheduler> logger)
        {
            _updateManager = updateManager ?? throw new ArgumentNullException(nameof(updateManager));
            _eligibilityEvaluator = eligibilityEvaluator ?? throw new ArgumentNullException(nameof(eligibilityEvaluator));
            _schedulerOptions = schedulerOptions?.Value ?? new SchedulerOptions();
            _deploymentOptions = deploymentOptions?.Value ?? new DeploymentOptions();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeDefaultTasks();
        }

        private void InitializeDefaultTasks()
        {
            // Update Check Task
            var checkTask = new ScheduledUpdateTask
            {
                TaskId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "UpdateCheck",
                IsRecurring = true,
                Interval = TimeSpan.FromMinutes(_schedulerOptions.CheckIntervalMinutes),
                NextRunTime = CalculateNextRunTimeWithJitter(TimeSpan.FromMinutes(_schedulerOptions.CheckIntervalMinutes))
            };
            _tasks.TryAdd(checkTask.TaskId, checkTask);

            // Download Task
            var downloadTask = new ScheduledUpdateTask
            {
                TaskId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Name = "Download",
                IsRecurring = true,
                Interval = TimeSpan.FromMinutes(_schedulerOptions.DownloadIntervalMinutes),
                NextRunTime = CalculateNextRunTimeWithJitter(TimeSpan.FromMinutes(_schedulerOptions.DownloadIntervalMinutes))
            };
            _tasks.TryAdd(downloadTask.TaskId, downloadTask);

            // Install Task
            var installTask = new ScheduledUpdateTask
            {
                TaskId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Name = "Install",
                IsRecurring = true,
                Interval = TimeSpan.FromMinutes(_schedulerOptions.InstallIntervalMinutes),
                NextRunTime = CalculateNextRunTimeWithJitter(TimeSpan.FromMinutes(_schedulerOptions.InstallIntervalMinutes))
            };
            _tasks.TryAdd(installTask.TaskId, installTask);
        }

        private DateTime CalculateNextRunTimeWithJitter(TimeSpan interval)
        {
            int maxJitter = _schedulerOptions.JitterSeconds;
            int jitterSeconds = Random.Shared.Next(-maxJitter, maxJitter + 1);
            return DateTime.UtcNow.Add(interval).AddSeconds(jitterSeconds);
        }

        public void Start()
        {
            if (_isStarted) return;
            _isStarted = true;

            _cts = new CancellationTokenSource();
            _schedulerLoopTask = Task.Run(() => SchedulerLoopAsync(_cts.Token));
            _logger.LogInformation("Enterprise Update Scheduler started.");
        }

        public void Stop()
        {
            if (!_isStarted) return;
            _isStarted = false;

            _cts?.Cancel();
            try
            {
                _schedulerLoopTask?.Wait();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while stopping Scheduler loop.");
            }
            _cts?.Dispose();
            _logger.LogInformation("Enterprise Update Scheduler stopped.");
        }

        public async Task TriggerImmediateCheckAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Triggering immediate scheduled update check.");
            var checkTask = _tasks.Values.FirstOrDefault(t => t.Name == "UpdateCheck");
            if (checkTask != null)
            {
                checkTask.NextRunTime = DateTime.UtcNow;
            }

            await ExecutePendingTasksAsync(cancellationToken);
        }

        public void ScheduleTask(ScheduledUpdateTask task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            _tasks.AddOrUpdate(task.TaskId, task, (id, oldTask) => task);
            _logger.LogInformation("Custom task {TaskName} (ID: {TaskId}) scheduled.", task.Name, task.TaskId);
        }

        public ScheduledUpdateTask[] GetScheduledTasks()
        {
            return _tasks.Values.OrderBy(t => t.NextRunTime).ToArray();
        }

        private async Task SchedulerLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ExecutePendingTasksAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    _logger.LogError(ex, "Exception in scheduler loop execution sweep.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ExecutePendingTasksAsync(CancellationToken cancellationToken)
        {
            if (!await _executionLock.WaitAsync(0, cancellationToken))
            {
                _logger.LogWarning("Overlapping execution detected; skipping scheduler sweep.");
                return;
            }

            try
            {
                var now = DateTime.UtcNow;
                var pendingTasks = _tasks.Values
                    .Where(t => t.NextRunTime <= now && !t.IsRunning)
                    .OrderBy(t => t.NextRunTime)
                    .ToList();

                foreach (var task in pendingTasks)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    task.IsRunning = true;
                    try
                    {
                        _logger.LogInformation("Executing scheduled update task: {TaskName}", task.Name);
                        await RunScheduledTaskActionAsync(task, cancellationToken);
                        task.LastRunTime = DateTime.UtcNow;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to execute scheduled update task: {TaskName}", task.Name);
                    }
                    finally
                    {
                        task.IsRunning = false;
                        if (task.IsRecurring)
                        {
                            task.NextRunTime = CalculateNextRunTimeWithJitter(task.Interval);
                        }
                    }
                }
            }
            finally
            {
                _executionLock.Release();
            }
        }

        private async Task RunScheduledTaskActionAsync(ScheduledUpdateTask task, CancellationToken cancellationToken)
        {
            switch (task.Name)
            {
                case "UpdateCheck":
                    var request = new UpdateCheckRequestDto
                    {
                        WorkstationId = "WS-SCHEDULER",
                        CurrentVersion = _deploymentOptions.CurrentVersion,
                        Channel = _deploymentOptions.Channel,
                        DeploymentRing = _deploymentOptions.Ring
                    };
                    var response = await _updateManager.CheckForUpdatesAsync(request, cancellationToken);
                    if (response != null && response.UpdateAvailable && response.Manifest != null)
                    {
                        _logger.LogInformation("Update check found available update: {TargetVersion}", response.Manifest.Version);

                        var manifest = new UpdateManifest
                        {
                            Id = response.Manifest.Id,
                            Version = response.Manifest.Version,
                            ProductName = response.Manifest.ProductName,
                            Description = response.Manifest.Description,
                            PackageType = response.Manifest.PackageType,
                            UpdateType = response.Manifest.UpdateType,
                            RequiredVersion = response.Manifest.RequiredVersion,
                            MinimumClientVersion = response.Manifest.MinimumClientVersion,
                            ReleaseDate = response.Manifest.ReleaseDate,
                            Priority = response.Manifest.Priority,
                            Channel = response.Manifest.Channel,
                            SignatureMetadata = response.Manifest.SignatureMetadata,
                            IsForcedUpgrade = response.Manifest.IsForcedUpgrade
                        };

                        var eligibility = await _eligibilityEvaluator.EvaluateEligibilityAsync(manifest, false, false, cancellationToken);
                        if (eligibility.IsEligible)
                        {
                            _logger.LogInformation("Workstation is eligible. Triggering update installation flow.");
                            await _updateManager.StartUpdateAsync(manifest, cancellationToken);
                        }
                        else
                        {
                            _logger.LogWarning("Workstation is ineligible for update: {Reasons}", string.Join(", ", eligibility.Reasons));
                        }
                    }
                    break;

                case "Download":
                    _logger.LogInformation("Download task executed - handled in coordination.");
                    break;

                case "Install":
                    _logger.LogInformation("Install task executed - handled in coordination.");
                    break;
            }
        }

        public void Dispose()
        {
            _cts?.Dispose();
            _executionLock.Dispose();
        }
    }
}
