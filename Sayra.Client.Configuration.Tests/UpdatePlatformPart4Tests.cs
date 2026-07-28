using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Sayra.Client.Shared.UpdatePlatform.Application.Dtos;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Services;
using Sayra.Client.Shared.UpdatePlatform.Application.Validation;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// Comprehensive test suite verifying update scheduling and deployment policy subsystem logic.
    /// </summary>
    public class UpdatePlatformPart4Tests
    {
        private readonly IVersionValidator _versionValidator = new VersionValidator();

        #region Maintenance Window Service Tests

        [Fact]
        public void MaintenanceWindowService_ShouldCorrectlyIdentifyInsideWindow()
        {
            // Arrange
            var options = new MaintenanceWindowOptions
            {
                StartTimeUtc = "01:00:00",
                EndTimeUtc = "03:00:00",
                AllowedDays = new List<string> { "Monday" },
                TimeZoneId = "UTC"
            };
            var service = new MaintenanceWindowService(Options.Create(options));

            // Monday, Jan 12, 2026 02:00:00 UTC
            var checkTime = new DateTime(2026, 1, 12, 2, 0, 0, DateTimeKind.Utc);

            // Act
            bool inside = service.IsInsideWindow(checkTime);

            // Assert
            Assert.True(inside);
        }

        [Fact]
        public void MaintenanceWindowService_ShouldRejectOutsideHours()
        {
            // Arrange
            var options = new MaintenanceWindowOptions
            {
                StartTimeUtc = "01:00:00",
                EndTimeUtc = "03:00:00",
                AllowedDays = new List<string> { "Monday" },
                TimeZoneId = "UTC"
            };
            var service = new MaintenanceWindowService(Options.Create(options));

            // Monday, Jan 12, 2026 04:00:00 UTC
            var checkTime = new DateTime(2026, 1, 12, 4, 0, 0, DateTimeKind.Utc);

            // Act
            bool inside = service.IsInsideWindow(checkTime);

            // Assert
            Assert.False(inside);
        }

        [Fact]
        public void MaintenanceWindowService_ShouldRejectOnExcludedDay()
        {
            // Arrange
            var options = new MaintenanceWindowOptions
            {
                StartTimeUtc = "01:00:00",
                EndTimeUtc = "03:00:00",
                AllowedDays = new List<string> { "Monday" },
                TimeZoneId = "UTC"
            };
            var service = new MaintenanceWindowService(Options.Create(options));

            // Tuesday, Jan 13, 2026 02:00:00 UTC
            var checkTime = new DateTime(2026, 1, 13, 2, 0, 0, DateTimeKind.Utc);

            // Act
            bool inside = service.IsInsideWindow(checkTime);

            // Assert
            Assert.False(inside);
        }

        [Fact]
        public void MaintenanceWindowService_ShouldRejectOnHolidays()
        {
            // Arrange
            var options = new MaintenanceWindowOptions
            {
                StartTimeUtc = "01:00:00",
                EndTimeUtc = "03:00:00",
                AllowedDays = new List<string> { "Monday" },
                HolidayExclusions = new List<string> { "2026-01-12" },
                TimeZoneId = "UTC"
            };
            var service = new MaintenanceWindowService(Options.Create(options));

            // Monday, Jan 12, 2026 02:00:00 UTC
            var checkTime = new DateTime(2026, 1, 12, 2, 0, 0, DateTimeKind.Utc);

            // Act
            bool inside = service.IsInsideWindow(checkTime);

            // Assert
            Assert.False(inside);
        }

        [Fact]
        public void MaintenanceWindowService_ShouldHandleCrossoverMidnight()
        {
            // Arrange
            var options = new MaintenanceWindowOptions
            {
                StartTimeUtc = "23:00:00",
                EndTimeUtc = "02:00:00",
                AllowedDays = new List<string> { "Monday", "Tuesday" },
                TimeZoneId = "UTC"
            };
            var service = new MaintenanceWindowService(Options.Create(options));

            var checkTimeBeforeMidnight = new DateTime(2026, 1, 12, 23, 30, 0, DateTimeKind.Utc);
            var checkTimeAfterMidnight = new DateTime(2026, 1, 13, 1, 15, 0, DateTimeKind.Utc);

            // Act & Assert
            Assert.True(service.IsInsideWindow(checkTimeBeforeMidnight));
            Assert.True(service.IsInsideWindow(checkTimeAfterMidnight));
        }

        [Fact]
        public void MaintenanceWindowService_EnsureInsideWindow_ShouldThrowViolationException()
        {
            // Arrange
            var options = new MaintenanceWindowOptions
            {
                StartTimeUtc = "01:00:00",
                EndTimeUtc = "03:00:00",
                TimeZoneId = "UTC"
            };
            var service = new MaintenanceWindowService(Options.Create(options));
            var checkTime = new DateTime(2026, 1, 12, 5, 0, 0, DateTimeKind.Utc);

            // Act & Assert
            Assert.Throws<MaintenanceWindowViolationException>(() => service.EnsureInsideWindow(checkTime));
        }

        [Fact]
        public void MaintenanceWindowService_EnsureInsideWindow_ShouldBypassIfForcedAndAllowed()
        {
            // Arrange
            var options = new MaintenanceWindowOptions
            {
                StartTimeUtc = "01:00:00",
                EndTimeUtc = "03:00:00",
                AllowForcedUpgrades = true,
                TimeZoneId = "UTC"
            };
            var service = new MaintenanceWindowService(Options.Create(options));
            var checkTime = new DateTime(2026, 1, 12, 5, 0, 0, DateTimeKind.Utc);

            // Act & Assert
            service.EnsureInsideWindow(checkTime, overrideForForced: true); // Should not throw
        }

        #endregion

        #region Deployment Policy Evaluator Tests

        [Fact]
        public void DeploymentPolicyEvaluator_ShouldBlockDeferredUpdates()
        {
            // Arrange
            var evaluator = new DeploymentPolicyEvaluator();
            var manifest = new UpdateManifest
            {
                ReleaseDate = DateTime.UtcNow.AddDays(-1)
            };
            var policy = new DeploymentPolicy
            {
                DeferralDays = 3,
                IsAutomatic = true
            };

            // Act & Assert
            Assert.Throws<DeploymentPolicyException>(() => evaluator.EvaluatePolicy(manifest, policy));
        }

        [Fact]
        public void DeploymentPolicyEvaluator_ShouldBlockManualApprovalUpdates()
        {
            // Arrange
            var evaluator = new DeploymentPolicyEvaluator();
            var manifest = new UpdateManifest { ReleaseDate = DateTime.UtcNow.AddDays(-5) };
            var policy = new DeploymentPolicy
            {
                RequiresApproval = true,
                IsAutomatic = false
            };

            // Act
            bool result = evaluator.EvaluatePolicy(manifest, policy);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void DeploymentPolicyEvaluator_ShouldAllowAutomaticUpdates()
        {
            // Arrange
            var evaluator = new DeploymentPolicyEvaluator();
            var manifest = new UpdateManifest { ReleaseDate = DateTime.UtcNow.AddDays(-5) };
            var policy = new DeploymentPolicy
            {
                IsAutomatic = true,
                RequiresApproval = false
            };

            // Act
            bool result = evaluator.EvaluatePolicy(manifest, policy);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region Rollout Service Tests

        [Fact]
        public void RolloutService_ShouldSupportPauseAndResumeAndCancel()
        {
            // Arrange
            var service = new RolloutService();
            var config = new RolloutConfiguration
            {
                CampaignId = Guid.NewGuid(),
                RolloutPercentage = 100
            };

            // 1. Pause check
            service.PauseRollout(config);
            Assert.True(config.IsPaused);
            Assert.Throws<RolloutRejectedException>(() => service.IsDeviceEligibleForRollout("WS-1", config));

            // 2. Resume check
            service.ResumeRollout(config);
            Assert.False(config.IsPaused);
            Assert.True(service.IsDeviceEligibleForRollout("WS-1", config));

            // 3. Cancel check
            service.CancelRollout(config);
            Assert.True(config.IsCancelled);
            Assert.Throws<RolloutRejectedException>(() => service.IsDeviceEligibleForRollout("WS-1", config));
        }

        [Fact]
        public void RolloutService_ShouldExcludesExplicitDeviceIds()
        {
            // Arrange
            var service = new RolloutService();
            var config = new RolloutConfiguration
            {
                RolloutPercentage = 100,
                ExcludedDeviceIds = new List<string> { "WS-BLACK-LISTED" }
            };

            // Act
            bool eligible = service.IsDeviceEligibleForRollout("WS-BLACK-LISTED", config);

            // Assert
            Assert.False(eligible);
        }

        [Fact]
        public void RolloutService_ShouldDeterministicSeedAndRolloutCorrectly()
        {
            // Arrange
            var service = new RolloutService();
            var config = new RolloutConfiguration
            {
                CampaignId = Guid.Parse("11112222-3333-4444-5555-666677778888"),
                RolloutPercentage = 30 // 30% rollout
            };

            // Act & Assert
            // Evaluate multiple clients. The same ID must always return the identical result deterministically.
            bool check1_1 = service.IsDeviceEligibleForRollout("WS-Client-42", config);
            bool check1_2 = service.IsDeviceEligibleForRollout("WS-Client-42", config);
            Assert.Equal(check1_1, check1_2);

            bool check2_1 = service.IsDeviceEligibleForRollout("WS-Client-105", config);
            bool check2_2 = service.IsDeviceEligibleForRollout("WS-Client-105", config);
            Assert.Equal(check2_1, check2_2);
        }

        #endregion

        #region Eligibility Evaluator Tests

        [Fact]
        public async Task EligibilityEvaluator_ShouldAcceptEligibleUpdates()
        {
            // Arrange
            var mockMW = new Mock<IMaintenanceWindowService>();
            mockMW.Setup(m => m.IsInsideWindow(It.IsAny<DateTime>())).Returns(true);

            var mockPolicy = new Mock<IDeploymentPolicyEvaluator>();
            mockPolicy.Setup(m => m.EvaluatePolicy(It.IsAny<UpdateManifest>(), It.IsAny<DeploymentPolicy>())).Returns(true);

            var depOptions = new DeploymentOptions
            {
                CurrentVersion = "1.0.0",
                Ring = DeploymentRing.Production,
                Channel = UpdateChannel.Stable
            };

            var evaluator = new EligibilityEvaluator(mockMW.Object, mockPolicy.Object, _versionValidator, Options.Create(depOptions));

            var manifest = new UpdateManifest
            {
                Version = "1.1.0",
                Channel = UpdateChannel.Stable,
                Priority = UpdatePriority.Normal
            };

            // Act
            var result = await evaluator.EvaluateEligibilityAsync(manifest, hasActiveSession: false, hasPendingOperations: false, CancellationToken.None);

            // Assert
            Assert.True(result.IsEligible);
            Assert.Empty(result.Reasons);
        }

        [Fact]
        public async Task EligibilityEvaluator_ShouldBlockDowngrades()
        {
            // Arrange
            var mockMW = new Mock<IMaintenanceWindowService>();
            mockMW.Setup(m => m.IsInsideWindow(It.IsAny<DateTime>())).Returns(true);

            var mockPolicy = new Mock<IDeploymentPolicyEvaluator>();
            mockPolicy.Setup(m => m.EvaluatePolicy(It.IsAny<UpdateManifest>(), It.IsAny<DeploymentPolicy>())).Returns(true);

            var depOptions = new DeploymentOptions
            {
                CurrentVersion = "2.0.0",
                Ring = DeploymentRing.Production,
                Channel = UpdateChannel.Stable
            };

            var evaluator = new EligibilityEvaluator(mockMW.Object, mockPolicy.Object, _versionValidator, Options.Create(depOptions));

            var manifest = new UpdateManifest
            {
                Version = "1.9.0",
                Channel = UpdateChannel.Stable,
                Priority = UpdatePriority.Normal
            };

            // Act
            var result = await evaluator.EvaluateEligibilityAsync(manifest, hasActiveSession: false, hasPendingOperations: false, CancellationToken.None);

            // Assert
            Assert.False(result.IsEligible);
            Assert.Contains(result.Reasons, r => r.Contains("Downgrades"));
        }

        [Fact]
        public async Task EligibilityEvaluator_ShouldFunnelBelowMinRequiredVersionToForcedUpdate()
        {
            // Arrange
            var mockMW = new Mock<IMaintenanceWindowService>();
            mockMW.Setup(m => m.IsInsideWindow(It.IsAny<DateTime>())).Returns(false); // Outside window!

            var mockPolicy = new Mock<IDeploymentPolicyEvaluator>();
            // Force return true for forced check
            mockPolicy.Setup(m => m.IsForcedUpdate(It.IsAny<UpdateManifest>(), It.IsAny<DeploymentPolicy>())).Returns(true);
            mockPolicy.Setup(m => m.EvaluatePolicy(It.IsAny<UpdateManifest>(), It.IsAny<DeploymentPolicy>())).Returns(true);

            var depOptions = new DeploymentOptions
            {
                CurrentVersion = "1.0.0",
                Ring = DeploymentRing.Production,
                Channel = UpdateChannel.Stable
            };

            var evaluator = new EligibilityEvaluator(mockMW.Object, mockPolicy.Object, _versionValidator, Options.Create(depOptions));

            var manifest = new UpdateManifest
            {
                Version = "2.0.0",
                MinimumClientVersion = "1.5.0", // Workstation (1.0.0) is below this!
                Channel = UpdateChannel.Stable,
                Priority = UpdatePriority.Normal
            };

            // Act
            var result = await evaluator.EvaluateEligibilityAsync(manifest, hasActiveSession: true, hasPendingOperations: true, CancellationToken.None);

            // Assert
            Assert.True(result.IsEligible); // Eligible despite active session & outside window because it's funneled to forced!
            Assert.Contains(result.Reasons, r => r.Contains("below the minimum required version"));
        }

        [Fact]
        public async Task EligibilityEvaluator_ShouldRestrictBetaChannelOnProductionRing()
        {
            // Arrange
            var mockMW = new Mock<IMaintenanceWindowService>();
            mockMW.Setup(m => m.IsInsideWindow(It.IsAny<DateTime>())).Returns(true);

            var mockPolicy = new Mock<IDeploymentPolicyEvaluator>();
            mockPolicy.Setup(m => m.EvaluatePolicy(It.IsAny<UpdateManifest>(), It.IsAny<DeploymentPolicy>())).Returns(true);

            var depOptions = new DeploymentOptions
            {
                CurrentVersion = "1.0.0",
                Ring = DeploymentRing.Production, // Client ring is Production
                Channel = UpdateChannel.Stable
            };

            var evaluator = new EligibilityEvaluator(mockMW.Object, mockPolicy.Object, _versionValidator, Options.Create(depOptions));

            var manifest = new UpdateManifest
            {
                Version = "1.1.0",
                Channel = UpdateChannel.Beta, // Update is Beta!
                Priority = UpdatePriority.Normal
            };

            // Act
            var result = await evaluator.EvaluateEligibilityAsync(manifest, hasActiveSession: false, hasPendingOperations: false, CancellationToken.None);

            // Assert
            Assert.False(result.IsEligible);
            Assert.Contains(result.Reasons, r => r.Contains("Beta updates are restricted"));
        }

        [Fact]
        public async Task EligibilityEvaluator_ShouldBlockDuringActiveSessionUnlessBypassed()
        {
            // Arrange
            var mockMW = new Mock<IMaintenanceWindowService>();
            mockMW.Setup(m => m.IsInsideWindow(It.IsAny<DateTime>())).Returns(true);

            var mockPolicy = new Mock<IDeploymentPolicyEvaluator>();
            mockPolicy.Setup(m => m.EvaluatePolicy(It.IsAny<UpdateManifest>(), It.IsAny<DeploymentPolicy>())).Returns(true);
            mockPolicy.Setup(m => m.IsForcedUpdate(It.IsAny<UpdateManifest>(), It.IsAny<DeploymentPolicy>())).Returns(false);

            var depOptions = new DeploymentOptions
            {
                CurrentVersion = "1.0.0",
                BypassActiveUserSession = false
            };

            var evaluator = new EligibilityEvaluator(mockMW.Object, mockPolicy.Object, _versionValidator, Options.Create(depOptions));

            var manifest = new UpdateManifest
            {
                Version = "1.1.0",
                Priority = UpdatePriority.Normal
            };

            // Act
            var result = await evaluator.EvaluateEligibilityAsync(manifest, hasActiveSession: true, hasPendingOperations: false, CancellationToken.None);

            // Assert
            Assert.False(result.IsEligible);
            Assert.Contains(result.Reasons, r => r.Contains("active user/game session"));
        }

        #endregion

        #region Update Scheduler Tests

        [Fact]
        public void UpdateScheduler_ShouldInitializeDefaultTasksAndPreventOverlap()
        {
            // Arrange
            var mockUpdateManager = new Mock<IUpdateManager>();
            var mockEligibility = new Mock<IEligibilityEvaluator>();
            var schedulerOptions = new SchedulerOptions
            {
                CheckIntervalMinutes = 180,
                DownloadIntervalMinutes = 60,
                InstallIntervalMinutes = 120,
                JitterSeconds = 300
            };
            var deploymentOptions = new DeploymentOptions();
            var logger = NullLogger<UpdateScheduler>.Instance;

            using (var scheduler = new UpdateScheduler(
                mockUpdateManager.Object,
                mockEligibility.Object,
                Options.Create(schedulerOptions),
                Options.Create(deploymentOptions),
                logger))
            {
                // Act
                var tasks = scheduler.GetScheduledTasks();

                // Assert
                Assert.Equal(3, tasks.Length);
                Assert.Contains(tasks, t => t.Name == "UpdateCheck" && t.Interval == TimeSpan.FromMinutes(180));
                Assert.Contains(tasks, t => t.Name == "Download" && t.Interval == TimeSpan.FromMinutes(60));
                Assert.Contains(tasks, t => t.Name == "Install" && t.Interval == TimeSpan.FromMinutes(120));
            }
        }

        [Fact]
        public void UpdateScheduler_ShouldAllowSchedulingManualTask()
        {
            // Arrange
            var mockUpdateManager = new Mock<IUpdateManager>();
            var mockEligibility = new Mock<IEligibilityEvaluator>();
            var logger = NullLogger<UpdateScheduler>.Instance;

            using (var scheduler = new UpdateScheduler(
                mockUpdateManager.Object,
                mockEligibility.Object,
                Options.Create(new SchedulerOptions()),
                Options.Create(new DeploymentOptions()),
                logger))
            {
                var customTask = new ScheduledUpdateTask
                {
                    TaskId = Guid.NewGuid(),
                    Name = "EmergencyCheck",
                    IsRecurring = false,
                    NextRunTime = DateTime.UtcNow.AddMinutes(5)
                };

                // Act
                scheduler.ScheduleTask(customTask);
                var tasks = scheduler.GetScheduledTasks();

                // Assert
                Assert.Contains(tasks, t => t.Name == "EmergencyCheck");
            }
        }

        #endregion
    }
}
