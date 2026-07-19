using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Diagnostics.Configuration;
using Sayra.Client.Diagnostics.Events;
using Sayra.Client.Diagnostics.Interfaces;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Services
{
    public class HardwareMonitoringService : BackgroundService, IHardwareMonitoringService
    {
        private readonly IHardwareSpecificationService _specService;
        private readonly IHardwareTelemetryService _telemetryService;
        private readonly IHardwareValidationService _validationService;
        private readonly IHardwareCacheService _cacheService;
        private readonly IOptions<DiagnosticsOptions> _options;
        private readonly ILogger<HardwareMonitoringService> _logger;

        private HardwareSpecification? _currentSpec;
        private HardwareMetrics? _currentMetrics;
        private readonly object _stateLock = new();

        public event EventHandler<TelemetryStartedEventArgs>? TelemetryStarted;
        public event EventHandler<TelemetryStoppedEventArgs>? TelemetryStopped;
        public event EventHandler<HardwareInitializedEventArgs>? HardwareInitialized;
        public event EventHandler<HardwareMetricsUpdatedEventArgs>? HardwareMetricsUpdated;
        public event EventHandler<HardwareValidationFailedEventArgs>? HardwareValidationFailed;
        public event EventHandler<HardwareChangedEventArgs>? HardwareChanged;
        public event EventHandler<DisplayChangedEventArgs>? DisplayChanged;
        public event EventHandler<NetworkChangedEventArgs>? NetworkChanged;

        public HardwareSpecification? CurrentSpecification
        {
            get { lock (_stateLock) { return _currentSpec; } }
        }

        public HardwareMetrics? CurrentMetrics
        {
            get { lock (_stateLock) { return _currentMetrics; } }
        }

        public HardwareMonitoringService(
            IHardwareSpecificationService specService,
            IHardwareTelemetryService telemetryService,
            IHardwareValidationService validationService,
            IHardwareCacheService cacheService,
            IOptions<DiagnosticsOptions> options,
            ILogger<HardwareMonitoringService> logger)
        {
            _specService = specService;
            _telemetryService = telemetryService;
            _validationService = validationService;
            _cacheService = cacheService;
            _options = options;
            _logger = logger;
        }

        public Task StartMonitoringAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("StartMonitoringAsync requested.");
            return StartAsync(cancellationToken);
        }

        public Task StopMonitoringAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("StopMonitoringAsync requested.");
            return StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Hardware Monitoring Background Service is starting...");
            TelemetryStarted?.Invoke(this, new TelemetryStartedEventArgs(DateTime.UtcNow));

            // Initialize hardware specifications
            await InitializeHardwareAsync(stoppingToken);

            int intervalMs = _options.Value.PollingIntervalMs;
            if (intervalMs < 100) intervalMs = 1000; // Safeguard minimum polling interval

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollAndValidateAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error occurred during hardware telemetry background polling loop. Attempting recovery on next tick.");
                }

                try
                {
                    await Task.Delay(intervalMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Hardware Monitoring Background Service is stopping.");
            TelemetryStopped?.Invoke(this, new TelemetryStoppedEventArgs(DateTime.UtcNow));
        }

        private async Task InitializeHardwareAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Loading hardware specification profile...");

                HardwareSpecification? spec = _cacheService.Get();
                if (spec == null)
                {
                    spec = await _specService.GetSpecificationAsync(cancellationToken);
                    _cacheService.Set(spec);
                }

                lock (_stateLock)
                {
                    _currentSpec = spec;
                }

                _logger.LogInformation("Hardware specification successfully initialized for CPU: '{Cpu}' with {Cores} logical cores.",
                    spec.Cpu.Name, spec.Cpu.LogicalCores);

                HardwareInitialized?.Invoke(this, new HardwareInitializedEventArgs(spec));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize hardware specification profile.");
            }
        }

        private async Task PollAndValidateAsync(CancellationToken cancellationToken)
        {
            // Collect live metrics
            var metrics = await _telemetryService.GetLiveMetricsAsync(cancellationToken);

            HardwareSpecification? spec;
            lock (_stateLock)
            {
                _currentMetrics = metrics;
                spec = _currentSpec;
            }

            HardwareMetricsUpdated?.Invoke(this, new HardwareMetricsUpdatedEventArgs(metrics));

            if (spec != null)
            {
                // Run validation
                var validationResult = await _validationService.ValidateAsync(spec, metrics, cancellationToken);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Hardware validation failed with {ErrorCount} errors.", validationResult.Errors.Count);
                    HardwareValidationFailed?.Invoke(this, new HardwareValidationFailedEventArgs(validationResult));
                }

                // Check for hardware changes and trigger events
                await CheckForDynamicChangesAsync(spec, cancellationToken);
            }
        }

        private async Task CheckForDynamicChangesAsync(HardwareSpecification lastSpec, CancellationToken cancellationToken)
        {
            // Verify if display, network, or other parameters have changed (e.g. plugging/unplugging monitors, Wi-Fi reconnection)
            try
            {
                if (_cacheService.IsExpired())
                {
                    _logger.LogDebug("Specification cache expired or renewal scheduled. Querying hardware spec for dynamic changes...");
                    var freshSpec = await _specService.GetSpecificationAsync(cancellationToken);
                    _cacheService.Set(freshSpec);

                    bool changed = false;

                    // 1. Check Display Changes
                    if (HasDisplaysChanged(lastSpec.Displays, freshSpec.Displays))
                    {
                        _logger.LogInformation("Display change detected!");
                        DisplayChanged?.Invoke(this, new DisplayChangedEventArgs(lastSpec.Displays, freshSpec.Displays));
                        changed = true;
                    }

                    // 2. Check Network Changes
                    if (HasNetworksChanged(lastSpec.Networks, freshSpec.Networks))
                    {
                        _logger.LogInformation("Network configuration change detected!");
                        NetworkChanged?.Invoke(this, new NetworkChangedEventArgs(lastSpec.Networks, freshSpec.Networks));
                        changed = true;
                    }

                    if (changed || lastSpec != freshSpec)
                    {
                        _logger.LogInformation("Hardware configuration change event fired.");
                        HardwareChanged?.Invoke(this, new HardwareChangedEventArgs(lastSpec, freshSpec));

                        lock (_stateLock)
                        {
                            _currentSpec = freshSpec;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to perform dynamic hardware change detection.");
            }
        }

        private bool HasDisplaysChanged(List<DisplayInformation> oldD, List<DisplayInformation> newD)
        {
            if (oldD.Count != newD.Count) return true;
            for (int i = 0; i < oldD.Count; i++)
            {
                if (oldD[i].Resolution != newD[i].Resolution || oldD[i].RefreshRate != newD[i].RefreshRate)
                    return true;
            }
            return false;
        }

        private bool HasNetworksChanged(List<NetworkInformation> oldN, List<NetworkInformation> newN)
        {
            if (oldN.Count != newN.Count) return true;
            for (int i = 0; i < oldN.Count; i++)
            {
                if (oldN[i].IPv4 != newN[i].IPv4 || oldN[i].MacAddress != newN[i].MacAddress)
                    return true;
            }
            return false;
        }
    }
}
