using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Events;
using Sayra.Client.Shared.Models.Recovery.Policies;
using SayraClient.Services.Recovery;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    public class ResilienceConfigurationTests : IDisposable
    {
        private readonly string _testConfigDir;
        private readonly string _testConfigFile;
        private readonly MockEventDispatcher _eventDispatcher;
        private readonly ResilienceConfigurationValidator _validator;

        public ResilienceConfigurationTests()
        {
            _testConfigDir = Path.Combine(AppContext.BaseDirectory, "TestConfigDir_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testConfigDir);
            _testConfigFile = Path.Combine(_testConfigDir, "resilience_config.json");
            _eventDispatcher = new MockEventDispatcher();
            _validator = new ResilienceConfigurationValidator();
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testConfigDir))
                {
                    Directory.Delete(_testConfigDir, true);
                }
            }
            catch { /* Ignore cleanup issues in tests */ }
        }

        #region Helper Mock Event Dispatcher
        private class MockEventDispatcher : IEventDispatcher
        {
            public readonly List<object> DispatchedEvents = new();
            private readonly List<Delegate> _handlers = new();

            public void Dispatch<T>(T @event)
            {
                lock (DispatchedEvents)
                {
                    DispatchedEvents.Add(@event!);
                }
            }

            public void RegisterHandler<T>(Action<T> handler)
            {
                _handlers.Add(handler);
            }
        }
        #endregion

        [Fact]
        public void Test_LoadDefaultConfiguration_CreatesValidDefaults()
        {
            // If the file doesn't exist, it should initialize with safe default configuration.
            Assert.False(File.Exists(_testConfigFile));

            var provider = new ResilienceConfigurationProvider(
                NullLogger<ResilienceConfigurationProvider>.Instance,
                _eventDispatcher,
                _validator,
                _testConfigFile);

            var config = provider.CurrentConfiguration;
            Assert.NotNull(config);
            Assert.Equal("1.0.0", config.SchemaVersion);
            Assert.True(config.SelfHealing.IsEnabled);
            Assert.True(File.Exists(_testConfigFile)); // Should have persisted the defaults

            // Verify a Loaded event was dispatched
            Assert.Single(_eventDispatcher.DispatchedEvents);
            Assert.IsType<ConfigurationLoadedEvent>(_eventDispatcher.DispatchedEvents[0]);
        }

        [Fact]
        public async Task Test_LoadJsonConfiguration_RetrievesSavedValues()
        {
            var customConfig = new ResilienceConfiguration
            {
                SchemaVersion = "1.0.0",
                Description = "Custom Testing Profile",
                Watchdog = new WatchdogOptions { PollingInterval = TimeSpan.FromSeconds(45) }
            };

            string json = JsonSerializer.Serialize(customConfig, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_testConfigFile, json);

            var provider = new ResilienceConfigurationProvider(
                NullLogger<ResilienceConfigurationProvider>.Instance,
                _eventDispatcher,
                _validator,
                _testConfigFile);

            var config = provider.CurrentConfiguration;
            Assert.NotNull(config);
            Assert.Equal("Custom Testing Profile", config.Description);
            Assert.Equal(TimeSpan.FromSeconds(45), config.Watchdog.PollingInterval);
        }

        [Fact]
        public void Test_ApplyEnvironmentOverrides_TakesPrecedence()
        {
            // Set environment variable overrides
            Environment.SetEnvironmentVariable("SAYRA_RESILIENCE_HEALTHMONITOR_BASE_DEDUCTION", "99.5");
            Environment.SetEnvironmentVariable("SAYRA_RESILIENCE_SELFHEALING_MAX_ATTEMPTS", "12");

            try
            {
                var provider = new ResilienceConfigurationProvider(
                    NullLogger<ResilienceConfigurationProvider>.Instance,
                    _eventDispatcher,
                    _validator,
                    _testConfigFile);

                var config = provider.CurrentConfiguration;
                Assert.Equal(99.5, config.HealthMonitor.BaseFailureDeduction);
                Assert.Equal(12, config.SelfHealing.MaxAttempts);
            }
            finally
            {
                // Clean up environment variables
                Environment.SetEnvironmentVariable("SAYRA_RESILIENCE_HEALTHMONITOR_BASE_DEDUCTION", null);
                Environment.SetEnvironmentVariable("SAYRA_RESILIENCE_SELFHEALING_MAX_ATTEMPTS", null);
            }
        }

        [Fact]
        public void Test_ValidateValidConfiguration_ReturnsSuccess()
        {
            var config = new ResilienceConfiguration();
            var result = _validator.Validate(config);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Test_RejectInvalidConfiguration_ReturnsErrors()
        {
            var config = new ResilienceConfiguration();
            // Create invalid ranges to trigger validator failure
            config.ResourceMonitor.CpuWarningThreshold = 95.0;
            config.ResourceMonitor.CpuCriticalThreshold = 90.0; // Warning is >= Critical!

            var result = _validator.Validate(config);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains("CpuWarningThreshold must be strictly less than CpuCriticalThreshold.", result.Errors[0]);
        }

        [Fact]
        public void Test_CrossOptionValidation_VerifyMetricHierarchies()
        {
            var config = new ResilienceConfiguration();

            // 1. Thread cross-validation Warning >= Critical
            config.ResourceMonitor.ThreadWarningThreshold = 200;
            config.ResourceMonitor.ThreadCriticalThreshold = 150;

            var result = _validator.Validate(config);
            Assert.False(result.IsValid);
            Assert.Contains("Thread thresholds must satisfy: Warning < Critical < Emergency.", result.Errors);

            // 2. System free memory cross-validation where Warning <= Critical (Representing available bytes)
            config.ResourceMonitor.ThreadWarningThreshold = 100;
            config.ResourceMonitor.ThreadCriticalThreshold = 150; // Restore thread values
            config.ResourceMonitor.SystemAvailableRamWarningBytes = 512 * 1024 * 1024L;
            config.ResourceMonitor.SystemAvailableRamCriticalBytes = 1024 * 1024 * 1024L; // Warning is <= Critical

            result = _validator.Validate(config);
            Assert.False(result.IsValid);
            Assert.Contains("SystemAvailableRamWarningBytes must be strictly greater than SystemAvailableRamCriticalBytes (since it represents free RAM threshold).", result.Errors);
        }

        [Fact]
        public async Task Test_AtomicReload_Success()
        {
            var provider = new ResilienceConfigurationProvider(
                NullLogger<ResilienceConfigurationProvider>.Instance,
                _eventDispatcher,
                _validator,
                _testConfigFile);

            // Verify initial description
            Assert.Equal("Default Production Resilience Configuration Profile", provider.CurrentConfiguration.Description);

            // Overwrite JSON file with updated description
            var newConfig = new ResilienceConfiguration
            {
                SchemaVersion = "1.0.0",
                Description = "Updated Real-Time Description"
            };
            string json = JsonSerializer.Serialize(newConfig, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_testConfigFile, json);

            // Execute reload
            bool reloaded = await provider.ReloadAsync();
            Assert.True(reloaded);
            Assert.Equal("Updated Real-Time Description", provider.CurrentConfiguration.Description);

            // Verify reloaded event was dispatched
            Assert.Contains(_eventDispatcher.DispatchedEvents, e => e is ConfigurationReloadedEvent);
        }

        [Fact]
        public async Task Test_AtomicReload_Failure_KeepsPreviousActive()
        {
            var provider = new ResilienceConfigurationProvider(
                NullLogger<ResilienceConfigurationProvider>.Instance,
                _eventDispatcher,
                _validator,
                _testConfigFile);

            var previousConfig = provider.CurrentConfiguration;

            // Write an invalid configuration to JSON file (invalid CPU thresholds)
            var invalidConfig = new ResilienceConfiguration
            {
                SchemaVersion = "1.0.0",
                Description = "Invalid Config Description"
            };
            invalidConfig.ResourceMonitor.CpuWarningThreshold = 99.0;
            invalidConfig.ResourceMonitor.CpuCriticalThreshold = 80.0; // Invalid! Warning >= Critical

            string json = JsonSerializer.Serialize(invalidConfig, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_testConfigFile, json);

            // Execute reload
            bool reloaded = await provider.ReloadAsync();
            Assert.False(reloaded);

            // Verify configuration was NOT updated (remained active previous configuration)
            Assert.Equal(previousConfig.Description, provider.CurrentConfiguration.Description);
            Assert.Equal(80.0, provider.CurrentConfiguration.ResourceMonitor.CpuWarningThreshold);

            // Verify failure event was dispatched
            Assert.Contains(_eventDispatcher.DispatchedEvents, e => e is ConfigurationValidationFailedEvent);
        }

        [Fact]
        public async Task Test_ConcurrentReaders_DuringReload()
        {
            var provider = new ResilienceConfigurationProvider(
                NullLogger<ResilienceConfigurationProvider>.Instance,
                _eventDispatcher,
                _validator,
                _testConfigFile);

            var running = true;
            var readerCount = 0;

            // Spawn concurrent reader threads
            var readers = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                readers.Add(Task.Run(() =>
                {
                    while (running)
                    {
                        var config = provider.CurrentConfiguration;
                        Assert.NotNull(config);
                        Interlocked.Increment(ref readerCount);
                    }
                }));
            }

            // Execute repeated atomic updates/reloads from another thread
            for (int i = 0; i < 20; i++)
            {
                var custom = new ResilienceConfiguration
                {
                    SchemaVersion = "1.0.0",
                    Description = $"Profile Run {i}"
                };
                await provider.UpdateConfigurationAsync(custom);
            }

            running = false;
            await Task.WhenAll(readers);

            Assert.True(readerCount > 100);
        }

        [Fact]
        public async Task Test_ConcurrentReloadAttempts_Safety()
        {
            var provider = new ResilienceConfigurationProvider(
                NullLogger<ResilienceConfigurationProvider>.Instance,
                _eventDispatcher,
                _validator,
                _testConfigFile);

            var reloadTasks = new List<Task<bool>>();
            for (int i = 0; i < 10; i++)
            {
                reloadTasks.Add(Task.Run(async () => await provider.ReloadAsync()));
            }

            var results = await Task.WhenAll(reloadTasks);
            foreach (var res in results)
            {
                Assert.True(res); // All concurrent reload attempts should finish successfully
            }
        }

        [Fact]
        public async Task Test_DynamicPolicyUpdate_PublishesPolicyUpdatedEvent()
        {
            var provider = new ResilienceConfigurationProvider(
                NullLogger<ResilienceConfigurationProvider>.Instance,
                _eventDispatcher,
                _validator,
                _testConfigFile);

            var newPolicy = new RecoveryPolicy
            {
                SubsystemName = "DynamicSubsystem",
                IsEnabled = true,
                Priority = RecoveryPriority.High,
                DefaultAction = RecoveryActionType.RestartIpc,
                Retry = new RetryPolicy { MaxRetries = 9 }
            };

            await provider.SavePolicyAsync(newPolicy);

            // Dynamic query should return the saved policy
            var policy = await provider.GetPolicyAsync("DynamicSubsystem");
            Assert.NotNull(policy);
            Assert.Equal(9, policy.Retry.MaxRetries);

            // Event check
            Assert.Contains(_eventDispatcher.DispatchedEvents, e => e is PolicyUpdatedEvent ev && ev.SubsystemName == "DynamicSubsystem");
        }

        [Fact]
        public void Test_SchemaMigration_AutomaticallyUpgradesOlderSupportedVersions()
        {
            // Write a version 0.9.0 config to disk
            var olderConfig = new ResilienceConfiguration
            {
                SchemaVersion = "0.9.0",
                Description = "Older profile version description"
            };
            string json = JsonSerializer.Serialize(olderConfig);
            File.WriteAllText(_testConfigFile, json);

            var provider = new ResilienceConfigurationProvider(
                NullLogger<ResilienceConfigurationProvider>.Instance,
                _eventDispatcher,
                _validator,
                _testConfigFile);

            // Should have been upgraded to 1.0.0
            Assert.Equal("1.0.0", provider.CurrentConfiguration.SchemaVersion);
            Assert.Contains("Migrated to 1.0.0", provider.CurrentConfiguration.Description);
        }

        [Fact]
        public void Test_SchemaMigration_FailsSafelyOnUnknownFutureVersions()
        {
            // Write an unknown version 2.0.0 config to disk
            var futureConfig = new ResilienceConfiguration
            {
                SchemaVersion = "2.0.0",
                Description = "Future Version Description"
            };
            string json = JsonSerializer.Serialize(futureConfig);
            File.WriteAllText(_testConfigFile, json);

            var provider = new ResilienceConfigurationProvider(
                NullLogger<ResilienceConfigurationProvider>.Instance,
                _eventDispatcher,
                _validator,
                _testConfigFile);

            // Provider must have failed to load version 2.0.0, falling back safely to built-in 1.0.0 defaults
            Assert.Equal("1.0.0", provider.CurrentConfiguration.SchemaVersion);
            Assert.Contains("Default Production Resilience Configuration Profile", provider.CurrentConfiguration.Description);
        }

        [Fact]
        public async Task Test_BackupAndRestore_FileIntegrityStrategy()
        {
            var backupFile = _testConfigFile + ".bak";
            Assert.False(File.Exists(backupFile));

            var provider = new ResilienceConfigurationProvider(
                NullLogger<ResilienceConfigurationProvider>.Instance,
                _eventDispatcher,
                _validator,
                _testConfigFile);

            var custom = new ResilienceConfiguration
            {
                SchemaVersion = "1.0.0",
                Description = "Persisted Custom Description"
            };

            await provider.UpdateConfigurationAsync(custom);

            // Backup file should be created automatically
            Assert.True(File.Exists(backupFile));

            // Corrupt the original config file with garbage
            await File.WriteAllTextAsync(_testConfigFile, " { MALFORMED GARBAGE {{");

            // Execute reload - should fail on corrupted config but restore/load the backup successfully
            bool reloaded = await provider.ReloadAsync();

            // Since reload validates JSON first and rejects if malformed, it preserves the existing in-memory profile
            Assert.False(reloaded);
            Assert.Equal("Persisted Custom Description", provider.CurrentConfiguration.Description);
        }

        [Fact]
        public void Test_PathValidation_BlocksTraversalAttacks()
        {
            // Test directory traversal validation check
            Assert.Throws<UnauthorizedAccessException>(() =>
            {
                var provider = new ResilienceConfigurationProvider(
                    NullLogger<ResilienceConfigurationProvider>.Instance,
                    _eventDispatcher,
                    _validator,
                    "../../traversal_resilience.json");
            });
        }

        [Fact]
        public void Test_SerializationRoundTrip_StatePreservation()
        {
            var original = new ResilienceConfiguration
            {
                SchemaVersion = "1.0.0",
                Description = "Roundtrip preservation test profile",
                Watchdog = new WatchdogOptions { PollingInterval = TimeSpan.FromSeconds(12) },
                CrashRecovery = new CrashRecoveryOptions { EnableDatabaseRepair = false }
            };

            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            var deserialized = JsonSerializer.Deserialize<ResilienceConfiguration>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(original.SchemaVersion, deserialized.SchemaVersion);
            Assert.Equal(original.Description, deserialized.Description);
            Assert.Equal(original.Watchdog.PollingInterval, deserialized.Watchdog.PollingInterval);
            Assert.Equal(original.CrashRecovery.EnableDatabaseRepair, deserialized.CrashRecovery.EnableDatabaseRepair);
        }
    }
}
