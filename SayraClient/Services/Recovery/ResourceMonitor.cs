using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Interfaces.Recovery.Providers;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Events;
using SayraClient.Services.Recovery.Providers.Windows;

namespace SayraClient.Services.Recovery
{
    /// <summary>
    /// Production-ready, thread-safe, enterprise-grade Resource Monitoring Engine.
    /// Monitors system resources, evaluates configurable thresholds, and publishes state events.
    /// </summary>
    public class ResourceMonitor : IResourceMonitor
    {
        private readonly ILogger<ResourceMonitor> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ResourceMonitorOptions _options;

        private readonly ICpuMetricsProvider _cpuProvider;
        private readonly IMemoryMetricsProvider _memoryProvider;
        private readonly IDiskMetricsProvider _diskProvider;
        private readonly INetworkMetricsProvider _networkProvider;
        private readonly IGpuMetricsProvider _gpuProvider;
        private readonly IProcessMetricsProvider _processProvider;

        // Subscriber registry for event subscriptions
        private readonly ConcurrentBag<Action<object>> _subscribers = new();

        // State tracking
        private ResourcePressureState _currentState = ResourcePressureState.Normal;
        private ResourcePressureState _previousState = ResourcePressureState.Normal;
        private DateTime _transitionTime = DateTime.UtcNow;
        private string _transitionReason = "Initial State";
        private readonly object _stateLock = new();

        // Background loop execution safety
        private int _monitoringActive = 0;

        // Legacy test-helper/simulation support (retained for backward compatibility)
        private double _simulatedCpu = 40.0;
        private long _simulatedRam = 250 * 1024 * 1024;
        private int _simulatedThreads = 25;
        private int _simulatedHandles = 300;
        private long _simulatedDisk = 5 * 1024 * 1024 * 1024L;
        private bool _isSimulated = false;

        /// <summary>
        /// Gets the current resource pressure state.
        /// </summary>
        public ResourcePressureState CurrentState
        {
            get { lock (_stateLock) return _currentState; }
        }

        /// <summary>
        /// Gets the previous resource pressure state.
        /// </summary>
        public ResourcePressureState PreviousState
        {
            get { lock (_stateLock) return _previousState; }
        }

        /// <summary>
        /// Gets the time of the last state transition.
        /// </summary>
        public DateTime TransitionTime
        {
            get { lock (_stateLock) return _transitionTime; }
        }

        /// <summary>
        /// Gets the reason for the last state transition.
        /// </summary>
        public string TransitionReason
        {
            get { lock (_stateLock) return _transitionReason; }
        }

        /// <summary>
        /// Backward-compatible constructor that resolves services from IServiceProvider or creates fallbacks.
        /// </summary>
        public ResourceMonitor(ILogger<ResourceMonitor> logger, IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            _eventDispatcher = (IEventDispatcher)serviceProvider.GetService(typeof(IEventDispatcher)) ?? new DummyEventDispatcher();

            var optsVal = (IOptions<ResourceMonitorOptions>)serviceProvider.GetService(typeof(IOptions<ResourceMonitorOptions>));
            _options = optsVal?.Value ?? new ResourceMonitorOptions();

            _cpuProvider = (ICpuMetricsProvider)serviceProvider.GetService(typeof(ICpuMetricsProvider))
                ?? new WindowsCpuMetricsProvider(CreateNullLogger<WindowsCpuMetricsProvider>());
            _memoryProvider = (IMemoryMetricsProvider)serviceProvider.GetService(typeof(IMemoryMetricsProvider))
                ?? new WindowsMemoryMetricsProvider(CreateNullLogger<WindowsMemoryMetricsProvider>());
            _diskProvider = (IDiskMetricsProvider)serviceProvider.GetService(typeof(IDiskMetricsProvider))
                ?? new WindowsDiskMetricsProvider(CreateNullLogger<WindowsDiskMetricsProvider>());
            _networkProvider = (INetworkMetricsProvider)serviceProvider.GetService(typeof(INetworkMetricsProvider))
                ?? new WindowsNetworkMetricsProvider(CreateNullLogger<WindowsNetworkMetricsProvider>());
            _gpuProvider = (IGpuMetricsProvider)serviceProvider.GetService(typeof(IGpuMetricsProvider))
                ?? new WindowsGpuMetricsProvider(CreateNullLogger<WindowsGpuMetricsProvider>());
            _processProvider = (IProcessMetricsProvider)serviceProvider.GetService(typeof(IProcessMetricsProvider))
                ?? new WindowsProcessMetricsProvider(CreateNullLogger<WindowsProcessMetricsProvider>());
        }

        /// <summary>
        /// Enterprise Constructor injection for unit testing.
        /// </summary>
        public ResourceMonitor(
            ILogger<ResourceMonitor> logger,
            IEventDispatcher eventDispatcher,
            IOptions<ResourceMonitorOptions> options,
            ICpuMetricsProvider cpuProvider,
            IMemoryMetricsProvider memoryProvider,
            IDiskMetricsProvider diskProvider,
            INetworkMetricsProvider networkProvider,
            IGpuMetricsProvider gpuProvider,
            IProcessMetricsProvider processProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            _cpuProvider = cpuProvider ?? throw new ArgumentNullException(nameof(cpuProvider));
            _memoryProvider = memoryProvider ?? throw new ArgumentNullException(nameof(memoryProvider));
            _diskProvider = diskProvider ?? throw new ArgumentNullException(nameof(diskProvider));
            _networkProvider = networkProvider ?? throw new ArgumentNullException(nameof(networkProvider));
            _gpuProvider = gpuProvider ?? throw new ArgumentNullException(nameof(gpuProvider));
            _processProvider = processProvider ?? throw new ArgumentNullException(nameof(processProvider));

            // Unused but kept for backward compatibility
            _serviceProvider = new DummyServiceProvider();
        }

        /// <summary>
        /// Configures simulated resources for testing/compatibility.
        /// </summary>
        public void SetSimulatedResources(double cpu, long ram, int threads, int handles, long diskBytes)
        {
            lock (_stateLock)
            {
                _simulatedCpu = cpu;
                _simulatedRam = ram;
                _simulatedThreads = threads;
                _simulatedHandles = handles;
                _simulatedDisk = diskBytes;
                _isSimulated = true;
            }
            _logger.LogInformation("ResourceMonitor simulation mode enabled with values CPU: {Cpu}%, RAM: {Ram}B, Threads: {Threads}, Handles: {Handles}, Disk: {Disk}B",
                cpu, ram, threads, handles, diskBytes);
        }

        /// <summary>
        /// Audits resource consumption. Preserved for backward compatibility.
        /// </summary>
        public async Task RunResourceAuditAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Performing legacy Resource Monitor Audit...");
            await RunResourceAuditInternalAsync(cancellationToken);

            // For backward compatibility with existing tests expecting cache cleanup
            var metrics = await GetCurrentMetricsAsync(cancellationToken);
            if (metrics.FreeDiskSpaceBytes < _options.DiskPressureBytes)
            {
                await TriggerAutomaticDiskCleanupAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Retrieves current workstation resource metrics. Preserved for backward compatibility.
        /// </summary>
        public Task<ResourceMetrics> GetResourceMetricsAsync(CancellationToken cancellationToken = default)
        {
            return GetCurrentMetricsAsync(cancellationToken);
        }

        /// <summary>
        /// Asynchronously retrieves current metrics from the providers or simulation.
        /// </summary>
        public async Task<ResourceMetrics> GetCurrentMetricsAsync(CancellationToken cancellationToken = default)
        {
            bool simulated;
            lock (_stateLock)
            {
                simulated = _isSimulated;
            }

            if (simulated)
            {
                return GetSimulatedSnapshot();
            }

            var correlationId = Guid.NewGuid().ToString("N");

            // Query each provider asynchronously with low-overhead and structured logging
            var cpu = await QueryAndLogAsync(correlationId, "CPU", _cpuProvider.GetCpuUsagePercentageAsync, 0.0, cancellationToken);
            var totalRam = await QueryAndLogAsync(correlationId, "TotalSystemRAM", _memoryProvider.GetTotalSystemRamBytesAsync, 16106127360L, cancellationToken);
            var availRam = await QueryAndLogAsync(correlationId, "AvailableSystemRAM", _memoryProvider.GetAvailableSystemRamBytesAsync, 8589934592L, cancellationToken);
            var freeDisk = await QueryAndLogAsync(correlationId, "FreeDiskSpace", ct => _diskProvider.GetFreeDiskSpaceBytesAsync(AppContext.BaseDirectory, ct), 10737418240L, cancellationToken);
            var diskIo = await QueryAndLogAsync(correlationId, "DiskIO", _diskProvider.GetDiskIoBytesPerSecondAsync, 0.0, cancellationToken);
            var networkIo = await QueryAndLogAsync(correlationId, "NetworkIO", _networkProvider.GetNetworkIoBytesPerSecondAsync, 0.0, cancellationToken);
            var gpu = await QueryAndLogAsync(correlationId, "GPU", _gpuProvider.GetGpuUsagePercentageAsync, 0.0, cancellationToken);
            var temp = await QueryAndLogAsync(correlationId, "HardwareTemperature", _gpuProvider.GetHardwareTemperatureCelsiusAsync, (double?)null, cancellationToken);
            var procRam = await QueryAndLogAsync(correlationId, "ProcessRAM", _processProvider.GetProcessRamBytesAsync, 0L, cancellationToken);
            var handles = await QueryAndLogAsync(correlationId, "HandleCount", _processProvider.GetHandleCountAsync, 0, cancellationToken);
            var threads = await QueryAndLogAsync(correlationId, "ThreadCount", _processProvider.GetThreadCountAsync, 0, cancellationToken);
            var gdi = await QueryAndLogAsync(correlationId, "GdiObjectsCount", _processProvider.GetGdiObjectsCountAsync, 0, cancellationToken);

            var (pressureLevel, status) = EvaluateMetrics(cpu, procRam, availRam, freeDisk, gpu, handles, threads, gdi, temp);

            return new ResourceMetrics
            {
                Timestamp = DateTime.UtcNow,
                MachineIdentifier = _options.MachineIdentifier,
                ThresholdStatus = status,
                CpuUsagePercentage = cpu,
                ProcessRamBytes = procRam,
                TotalSystemRamBytes = totalRam,
                AvailableSystemRamBytes = availRam,
                FreeDiskSpaceBytes = freeDisk,
                HandleCount = handles,
                ThreadCount = threads,
                GdiObjectsCount = gdi,
                GpuUsagePercentage = gpu,
                DiskIoBytesPerSecond = diskIo,
                NetworkIoBytesPerSecond = networkIo,
                HardwareTemperatureCelsius = temp,
                PressureLevel = pressureLevel
            };
        }

        /// <summary>
        /// Asynchronously retrieves a snapshot of current metrics.
        /// </summary>
        public Task<ResourceMetrics> GetResourceSnapshotAsync(CancellationToken cancellationToken = default)
        {
            return GetCurrentMetricsAsync(cancellationToken);
        }

        /// <summary>
        /// Executes background resource monitoring with a sampling interval.
        /// </summary>
        public async Task MonitorAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _monitoringActive, 1, 0) != 0)
            {
                _logger.LogWarning("MonitorAsync was called but background monitoring loop is already running.");
                return;
            }

            _logger.LogInformation("Starting background resource monitoring loop. Interval: {Interval}", _options.SamplingInterval);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await RunResourceAuditInternalAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception in resource monitor sampling iteration.");
                    }

                    await Task.Delay(_options.SamplingInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful cancellation exit
            }
            finally
            {
                Interlocked.Exchange(ref _monitoringActive, 0);
                _logger.LogInformation("Background resource monitoring loop stopped.");
            }
        }

        /// <summary>
        /// Subscribes thread-safely to resource monitoring events.
        /// </summary>
        public Task SubscribeToResourceEvents(Action<object> handler, CancellationToken cancellationToken = default)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _subscribers.Add(handler);
            return Task.CompletedTask;
        }

        private async Task RunResourceAuditInternalAsync(CancellationToken cancellationToken)
        {
            var metrics = await GetCurrentMetricsAsync(cancellationToken);
            var state = MapPressureToState(metrics.PressureLevel);
            UpdateState(state, metrics.ThresholdStatus, metrics);

            // Publish metrics collected event
            var correlationId = Guid.NewGuid().ToString("N");
            DispatchEvent(new ResourceMetricsCollectedEvent(correlationId, metrics, DateTime.UtcNow));
        }

        private async Task<T> QueryAndLogAsync<T>(
            string correlationId,
            string resourceType,
            Func<CancellationToken, Task<T>> queryFunc,
            T fallbackValue,
            CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var value = await queryFunc(cancellationToken);
                sw.Stop();
                _logger.LogInformation(
                    "Resource collection - CorrelationId: {CorrelationId}, Operation: {Operation}, ResourceType: {ResourceType}, Value: {Value}, Duration: {DurationMs}ms, Result: {Result}",
                    correlationId, "QueryMetric", resourceType, value, sw.ElapsedMilliseconds, "Success");
                return value;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Resource collection failed - CorrelationId: {CorrelationId}, Operation: {Operation}, ResourceType: {ResourceType}, Duration: {DurationMs}ms, Result: {Result}",
                    correlationId, "QueryMetric", resourceType, sw.ElapsedMilliseconds, "Failure");
                return fallbackValue;
            }
        }

        private (ResourcePressureLevel, string) EvaluateMetrics(
            double cpu, long procRam, long availRam, long freeDisk, double gpu, int handles, int threads, int gdi, double? temp)
        {
            ResourcePressureLevel level = ResourcePressureLevel.Normal;
            string status = "Normal";

            // Check Emergency Thresholds
            if (cpu >= _options.CpuEmergencyThreshold)
            {
                level = ResourcePressureLevel.Critical;
                status = $"CPU usage at {cpu:F1}% crossed Emergency threshold of {_options.CpuEmergencyThreshold}%";
            }
            else if (procRam >= _options.ProcessRamEmergencyBytes)
            {
                level = ResourcePressureLevel.Critical;
                status = $"Process working set RAM at {procRam / (1024.0 * 1024.0):F1}MB crossed Emergency threshold of {_options.ProcessRamEmergencyBytes / (1024 * 1024)}MB";
            }
            else if (availRam <= _options.SystemAvailableRamEmergencyBytes)
            {
                level = ResourcePressureLevel.Critical;
                status = $"System available RAM at {availRam / (1024.0 * 1024.0):F1}MB fell below Emergency threshold of {_options.SystemAvailableRamEmergencyBytes / (1024 * 1024)}MB";
            }
            else if (gpu >= _options.GpuEmergencyThreshold)
            {
                level = ResourcePressureLevel.Critical;
                status = $"GPU usage at {gpu:F1}% crossed Emergency threshold of {_options.GpuEmergencyThreshold}%";
            }
            else if (handles >= _options.HandleEmergencyThreshold)
            {
                level = ResourcePressureLevel.Critical;
                status = $"Process handle count at {handles} crossed Emergency threshold of {_options.HandleEmergencyThreshold}";
            }
            else if (threads >= _options.ThreadEmergencyThreshold)
            {
                level = ResourcePressureLevel.Critical;
                status = $"Process thread count at {threads} crossed Emergency threshold of {_options.ThreadEmergencyThreshold}";
            }
            else if (gdi >= _options.GdiEmergencyThreshold)
            {
                level = ResourcePressureLevel.Critical;
                status = $"Process GDI objects count at {gdi} crossed Emergency threshold of {_options.GdiEmergencyThreshold}";
            }

            if (level == ResourcePressureLevel.Critical) return (level, status);

            // Check Critical Thresholds
            if (cpu >= _options.CpuCriticalThreshold)
            {
                level = ResourcePressureLevel.Critical;
                status = $"CPU usage at {cpu:F1}% crossed Critical threshold of {_options.CpuCriticalThreshold}%";
            }
            else if (procRam >= _options.ProcessRamCriticalBytes)
            {
                level = ResourcePressureLevel.Critical;
                status = $"Process working set RAM at {procRam / (1024.0 * 1024.0):F1}MB crossed Critical threshold of {_options.ProcessRamCriticalBytes / (1024 * 1024)}MB";
            }
            else if (availRam <= _options.SystemAvailableRamCriticalBytes)
            {
                level = ResourcePressureLevel.Critical;
                status = $"System available RAM at {availRam / (1024.0 * 1024.0):F1}MB fell below Critical threshold of {_options.SystemAvailableRamCriticalBytes / (1024 * 1024)}MB";
            }
            else if (gpu >= _options.GpuCriticalThreshold)
            {
                level = ResourcePressureLevel.Critical;
                status = $"GPU usage at {gpu:F1}% crossed Critical threshold of {_options.GpuCriticalThreshold}%";
            }
            else if (handles >= _options.HandleCriticalThreshold)
            {
                level = ResourcePressureLevel.Critical;
                status = $"Process handle count at {handles} crossed Critical threshold of {_options.HandleCriticalThreshold}";
            }
            else if (threads >= _options.ThreadCriticalThreshold)
            {
                level = ResourcePressureLevel.Critical;
                status = $"Process thread count at {threads} crossed Critical threshold of {_options.ThreadCriticalThreshold}";
            }
            else if (gdi >= _options.GdiCriticalThreshold)
            {
                level = ResourcePressureLevel.Critical;
                status = $"Process GDI objects count at {gdi} crossed Critical threshold of {_options.GdiCriticalThreshold}";
            }
            else if (temp >= _options.TemperatureCriticalThreshold)
            {
                level = ResourcePressureLevel.Critical;
                status = $"Hardware temperature at {temp:F1}°C crossed Critical threshold of {_options.TemperatureCriticalThreshold}°C";
            }

            if (level == ResourcePressureLevel.Critical) return (level, status);

            // Check Warning / Disk Pressure Thresholds
            if (cpu >= _options.CpuWarningThreshold)
            {
                level = ResourcePressureLevel.High; // Maps to Warning state
                status = $"CPU usage at {cpu:F1}% crossed Warning threshold of {_options.CpuWarningThreshold}%";
            }
            else if (procRam >= _options.ProcessRamWarningBytes)
            {
                level = ResourcePressureLevel.High;
                status = $"Process working set RAM at {procRam / (1024.0 * 1024.0):F1}MB crossed Warning threshold of {_options.ProcessRamWarningBytes / (1024 * 1024)}MB";
            }
            else if (availRam <= _options.SystemAvailableRamWarningBytes)
            {
                level = ResourcePressureLevel.High;
                status = $"System available RAM at {availRam / (1024.0 * 1024.0):F1}MB fell below Warning threshold of {_options.SystemAvailableRamWarningBytes / (1024 * 1024)}MB";
            }
            else if (gpu >= _options.GpuWarningThreshold)
            {
                level = ResourcePressureLevel.High;
                status = $"GPU usage at {gpu:F1}% crossed Warning threshold of {_options.GpuWarningThreshold}%";
            }
            else if (handles >= _options.HandleWarningThreshold)
            {
                level = ResourcePressureLevel.High;
                status = $"Process handle count at {handles} crossed Warning threshold of {_options.HandleWarningThreshold}";
            }
            else if (threads >= _options.ThreadWarningThreshold)
            {
                level = ResourcePressureLevel.High;
                status = $"Process thread count at {threads} crossed Warning threshold of {_options.ThreadWarningThreshold}";
            }
            else if (gdi >= _options.GdiWarningThreshold)
            {
                level = ResourcePressureLevel.High;
                status = $"Process GDI objects count at {gdi} crossed Warning threshold of {_options.GdiWarningThreshold}";
            }
            else if (temp >= _options.TemperatureWarningThreshold)
            {
                level = ResourcePressureLevel.High;
                status = $"Hardware temperature at {temp:F1}°C crossed Warning threshold of {_options.TemperatureWarningThreshold}°C";
            }
            else if (freeDisk < _options.DiskPressureBytes)
            {
                level = ResourcePressureLevel.Medium; // Maps to Warning/Medium state
                status = $"Free disk space at {freeDisk / (1024.0 * 1024.0):F1}MB fell below disk pressure threshold of {_options.DiskPressureBytes / (1024 * 1024)}MB";
            }

            return (level, status);
        }

        private void UpdateState(ResourcePressureState newState, string reason, ResourceMetrics metrics)
        {
            ResourcePressureState oldState;
            lock (_stateLock)
            {
                if (_currentState == newState) return;

                _previousState = _currentState;
                _currentState = newState;
                _transitionTime = DateTime.UtcNow;
                _transitionReason = reason;
                oldState = _previousState;
            }

            _logger.LogWarning(
                "Resource Monitor state transition: PreviousState={PreviousState}, CurrentState={CurrentState}, TransitionTime={TransitionTime}, Reason={Reason}",
                oldState, newState, DateTime.UtcNow, reason);

            var correlationId = Guid.NewGuid().ToString("N");

            // Dispatch threshold exceeded event (always when a transition occurs)
            var exceededVal = GetExceededMetricValue(metrics, reason);
            var exceededThreshold = GetExceededMetricThreshold(reason);
            var resType = GetExceededResourceType(reason);

            DispatchEvent(new ResourceThresholdExceededEvent(
                correlationId,
                resType,
                exceededVal,
                exceededThreshold,
                newState.ToString(),
                DateTime.UtcNow));

            // State changes dispatch warning/recovery events
            if (newState != ResourcePressureState.Normal && oldState == ResourcePressureState.Normal)
            {
                DispatchEvent(new ResourcePressureDetectedEvent(
                    correlationId,
                    resType,
                    exceededVal,
                    exceededThreshold,
                    newState.ToString(),
                    DateTime.UtcNow));
            }
            else if (newState == ResourcePressureState.Normal && oldState != ResourcePressureState.Normal)
            {
                DispatchEvent(new ResourcePressureRecoveredEvent(
                    correlationId,
                    "System",
                    0.0,
                    0.0,
                    DateTime.UtcNow));
            }
        }

        private static ResourcePressureState MapPressureToState(ResourcePressureLevel level)
        {
            return level switch
            {
                ResourcePressureLevel.Normal => ResourcePressureState.Normal,
                ResourcePressureLevel.Low => ResourcePressureState.Normal,
                ResourcePressureLevel.Medium => ResourcePressureState.Warning,
                ResourcePressureLevel.High => ResourcePressureState.Warning,
                ResourcePressureLevel.Critical => ResourcePressureState.Critical,
                _ => ResourcePressureState.Normal
            };
        }

        private ResourceMetrics GetSimulatedSnapshot()
        {
            double cpu;
            long ram;
            int threads;
            int handles;
            long disk;

            lock (_stateLock)
            {
                cpu = _simulatedCpu;
                ram = _simulatedRam;
                threads = _simulatedThreads;
                handles = _simulatedHandles;
                disk = _simulatedDisk;
            }

            var (level, status) = EvaluateMetrics(cpu, ram, 4294967296L, disk, 5.0, handles, threads, 120, 45.0);

            return new ResourceMetrics
            {
                Timestamp = DateTime.UtcNow,
                MachineIdentifier = _options?.MachineIdentifier ?? "WS-RESOURCE-MONITOR",
                ThresholdStatus = status,
                CpuUsagePercentage = cpu,
                ProcessRamBytes = ram,
                TotalSystemRamBytes = 8589934592L,
                AvailableSystemRamBytes = 4294967296L,
                FreeDiskSpaceBytes = disk,
                HandleCount = handles,
                ThreadCount = threads,
                GdiObjectsCount = 120,
                GpuUsagePercentage = 5.0,
                DiskIoBytesPerSecond = 1024 * 50,
                NetworkIoBytesPerSecond = 1024 * 100,
                HardwareTemperatureCelsius = 45.0,
                PressureLevel = level
            };
        }

        private void DispatchEvent<T>(T @event) where T : class
        {
            try
            {
                _eventDispatcher?.Dispatch(@event);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch resource event via IEventDispatcher.");
            }

            foreach (var subscriber in _subscribers)
            {
                try
                {
                    subscriber(@event);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to invoke local resource event subscriber.");
                }
            }
        }

        private static string GetExceededResourceType(string reason)
        {
            if (reason.Contains("CPU")) return "CPU";
            if (reason.Contains("RAM") || reason.Contains("memory")) return "Memory";
            if (reason.Contains("disk") || reason.Contains("Disk")) return "Disk";
            if (reason.Contains("GPU")) return "GPU";
            if (reason.Contains("handle")) return "Handles";
            if (reason.Contains("thread")) return "Threads";
            if (reason.Contains("GDI")) return "GdiObjects";
            if (reason.Contains("temperature")) return "Temperature";
            return "System";
        }

        private static double GetExceededMetricValue(ResourceMetrics metrics, string reason)
        {
            if (reason.Contains("CPU")) return metrics.CpuUsagePercentage;
            if (reason.Contains("Process working set RAM")) return metrics.ProcessRamBytes;
            if (reason.Contains("System available RAM")) return metrics.AvailableSystemRamBytes;
            if (reason.Contains("disk")) return metrics.FreeDiskSpaceBytes;
            if (reason.Contains("GPU")) return metrics.GpuUsagePercentage;
            if (reason.Contains("handle")) return metrics.HandleCount;
            if (reason.Contains("thread")) return metrics.ThreadCount;
            if (reason.Contains("GDI")) return metrics.GdiObjectsCount;
            if (reason.Contains("temperature")) return metrics.HardwareTemperatureCelsius ?? 0.0;
            return 0.0;
        }

        private double GetExceededMetricThreshold(string reason)
        {
            if (reason.Contains("CPU"))
            {
                if (reason.Contains("Emergency")) return _options.CpuEmergencyThreshold;
                if (reason.Contains("Critical")) return _options.CpuCriticalThreshold;
                return _options.CpuWarningThreshold;
            }
            if (reason.Contains("Process working set RAM"))
            {
                if (reason.Contains("Emergency")) return _options.ProcessRamEmergencyBytes;
                if (reason.Contains("Critical")) return _options.ProcessRamCriticalBytes;
                return _options.ProcessRamWarningBytes;
            }
            if (reason.Contains("System available RAM"))
            {
                if (reason.Contains("Emergency")) return _options.SystemAvailableRamEmergencyBytes;
                if (reason.Contains("Critical")) return _options.SystemAvailableRamCriticalBytes;
                return _options.SystemAvailableRamWarningBytes;
            }
            if (reason.Contains("disk")) return _options.DiskPressureBytes;
            if (reason.Contains("GPU"))
            {
                if (reason.Contains("Emergency")) return _options.GpuEmergencyThreshold;
                if (reason.Contains("Critical")) return _options.GpuCriticalThreshold;
                return _options.GpuWarningThreshold;
            }
            if (reason.Contains("handle"))
            {
                if (reason.Contains("Emergency")) return _options.HandleEmergencyThreshold;
                if (reason.Contains("Critical")) return _options.HandleCriticalThreshold;
                return _options.HandleWarningThreshold;
            }
            if (reason.Contains("thread"))
            {
                if (reason.Contains("Emergency")) return _options.ThreadEmergencyThreshold;
                if (reason.Contains("Critical")) return _options.ThreadCriticalThreshold;
                return _options.ThreadWarningThreshold;
            }
            if (reason.Contains("GDI"))
            {
                if (reason.Contains("Emergency")) return _options.GdiEmergencyThreshold;
                if (reason.Contains("Critical")) return _options.GdiCriticalThreshold;
                return _options.GdiWarningThreshold;
            }
            if (reason.Contains("temperature"))
            {
                if (reason.Contains("Critical")) return _options.TemperatureCriticalThreshold;
                return _options.TemperatureWarningThreshold;
            }
            return 0.0;
        }

        private async Task TriggerAutomaticDiskCleanupAsync(CancellationToken ct)
        {
            _logger.LogWarning("DISK CRITICAL LOW: Triggering automatic disk cleanup.");
            try
            {
                var cache = (IAdvertisementCache)_serviceProvider?.GetService(typeof(IAdvertisementCache));
                if (cache != null)
                {
                    // Clean up 200MB of LRU cache
                    long requiredBytes = 200 * 1024 * 1024;
                    await cache.EvictLeastRecentlyUsedAsync(requiredBytes, ct);
                    await cache.ClearExpiredCacheAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run automatic disk cleanup on low disk alert.");
            }
        }

        private static ILogger<T> CreateNullLogger<T>() => Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance;

        private class DummyEventDispatcher : IEventDispatcher
        {
            public void Dispatch<T>(T @event) { }
            public void RegisterHandler<T>(Action<T> handler) { }
        }

        private class DummyServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType) => null!;
        }
    }
}
