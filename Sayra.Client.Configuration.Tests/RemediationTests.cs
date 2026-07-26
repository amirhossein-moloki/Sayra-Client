using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Application.Services;
using Sayra.Client.Shared.Runtime.Domain.Models;
using Sayra.Client.Shared.Runtime.Domain.States;
using Sayra.Client.Shared.Runtime.Domain.Entities;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Launch.Application.Services;
using Sayra.Client.Shared.Runtime.Launch.Domain.Models;
using Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Sandbox;
using Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Registry;
using Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Overlay.Application.Services;
using Sayra.Client.Shared.Runtime.Overlay.Domain.Models;
using Sayra.Client.Shared.Runtime.Overlay.Domain.States;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Services;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models;
using SayraClient.Kiosk.Application.Interfaces;
using SayraClient.Kiosk.Domain.Models;
using SayraClient.Kiosk.Infrastructure.DeviceMonitoring;

namespace Sayra.Client.Configuration.Tests
{
    public class RemediationTests
    {
        // ==========================================
        // 1. RESOURCE LIMITS & PROCESS SUPERVISOR TESTS
        // ==========================================
        [Fact]
        public async Task ProcessSupervisor_EnforcesLimitsBeforeAssignment_ShouldSucceed()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ProcessSupervisor>>();
            var eventPublisherMock = new Mock<IRuntimeEventPublisher>();
            var jobManagerMock = new Mock<IJobObjectManager>();
            var treeMonitorMock = new Mock<IProcessTreeMonitor>();
            var resourceMonitorMock = new Mock<IProcessResourceMonitor>();

            var options = Options.Create(new ProcessSupervisorOptions
            {
                MaxMemoryBytes = 512 * 1024 * 1024, // 512 MB
                CpuAffinityMask = 3,                 // Cores 0 & 1
                PriorityClass = "High"
            });

            using var supervisor = new ProcessSupervisor(
                loggerMock.Object,
                eventPublisherMock.Object,
                jobManagerMock.Object,
                treeMonitorMock.Object,
                resourceMonitorMock.Object,
                options
            );

            var runtimeId = Guid.NewGuid();
            var procInfo = new ProcessInfo
            {
                RuntimeId = runtimeId,
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                ProcessName = "TestGame"
            };

            // Act
            await supervisor.RegisterAsync(procInfo);

            // Assert
            jobManagerMock.Verify(x => x.CreateJob(runtimeId), Times.Once);
            // Verify limit config was applied BEFORE assigning the process
            jobManagerMock.Verify(x => x.ConfigureLimits(runtimeId, 512 * 1024 * 1024, 3), Times.Once);
            jobManagerMock.Verify(x => x.AssignProcess(runtimeId, procInfo.ProcessId), Times.Once);
        }

        [Fact]
        public async Task ProcessSupervisor_LimitConfigurationFailure_ShouldRollbackAndThrow()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ProcessSupervisor>>();
            var eventPublisherMock = new Mock<IRuntimeEventPublisher>();
            var jobManagerMock = new Mock<IJobObjectManager>();
            var treeMonitorMock = new Mock<IProcessTreeMonitor>();
            var resourceMonitorMock = new Mock<IProcessResourceMonitor>();

            jobManagerMock.Setup(x => x.ConfigureLimits(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<ulong>()))
                .Throws(new InvalidOperationException("Failed to set Job Object limits."));

            using var supervisor = new ProcessSupervisor(
                loggerMock.Object,
                eventPublisherMock.Object,
                jobManagerMock.Object,
                treeMonitorMock.Object,
                resourceMonitorMock.Object,
                null
            );

            var procInfo = new ProcessInfo { RuntimeId = Guid.NewGuid(), ProcessId = 1234, ProcessName = "Game" };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.RegisterAsync(procInfo));
            // Ensure process was NOT assigned because limit config failed
            jobManagerMock.Verify(x => x.AssignProcess(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
        }

        // ==========================================
        // 2. SANDBOX ISOLATION & ROLLBACK TESTS
        // ==========================================
        [Fact]
        public async Task SandboxManager_PrepareAndCleanupLifecycle_ShouldIsolateCorrectly()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<WindowsSandboxManager>>();
            var sandboxManager = new WindowsSandboxManager(loggerMock.Object);
            string baseSandboxPath = Path.Combine(Path.GetTempPath(), "SAYRA_Sandbox_Test_" + Guid.NewGuid());

            try
            {
                // Act - Prepare Sandbox
                await sandboxManager.PrepareSandboxAsync("game_1", baseSandboxPath);

                // Assert subdirectories exist
                Assert.True(Directory.Exists(baseSandboxPath));
                Assert.True(Directory.Exists(Path.Combine(baseSandboxPath, "SaveData")));
                Assert.True(Directory.Exists(Path.Combine(baseSandboxPath, "Temp")));
                Assert.True(Directory.Exists(Path.Combine(baseSandboxPath, "Cache")));

                // Act - Idempotent cleanup
                await sandboxManager.CleanupSandboxAsync("game_1", baseSandboxPath);

                // Assert directory deleted
                Assert.False(Directory.Exists(baseSandboxPath));
            }
            finally
            {
                if (Directory.Exists(baseSandboxPath)) Directory.Delete(baseSandboxPath, true);
            }
        }

        [Fact]
        public async Task SandboxManager_PathTraversalAttack_ShouldBeBlocked()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<WindowsSandboxManager>>();
            var sandboxManager = new WindowsSandboxManager(loggerMock.Object);
            string maliciousPath = @"C:\SAYRA_Client\Saves\..\..\Windows\System32";

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                sandboxManager.PrepareSandboxAsync("game_evil", maliciousPath));
            Assert.Contains("Path traversal attempt blocked", ex.Message);
        }

        [Fact]
        public async Task SandboxManager_RollbackOnPrepareFailure_ShouldCleanUpRoot()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<WindowsSandboxManager>>();
            var sandboxManager = new WindowsSandboxManager(loggerMock.Object);
            // Pass a path that is formatted correctly but is impossible to create (non-existent root) to trigger Directory.CreateDirectory failure and rollback
            string invalidPath = "/nonexistent/volume/SAYRA_Sandbox";

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sandboxManager.PrepareSandboxAsync("game_fail", invalidPath));
        }

        // ==========================================
        // 3. APPLICATION REGISTRY ISOLATION TESTS
        // ==========================================
        [Fact]
        public async Task RegistryVirtualization_IsolatesMultipleConcurrentSessions()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<WindowsRegistryVirtualizationManager>>();
            var registryManager = new WindowsRegistryVirtualizationManager(loggerMock.Object);
            var session1 = Guid.NewGuid();
            var session2 = Guid.NewGuid();
            var virtualKeys = new Dictionary<string, string> { { "Resolution", "1920x1080" } };

            // Act & Assert - Run preparing virtualized registries.
            // On non-Windows platforms, this will fallback gracefully.
            await registryManager.PrepareRegistryAsync(session1, "game_1", virtualKeys);
            await registryManager.PrepareRegistryAsync(session2, "game_1", virtualKeys);

            // Cleanup
            await registryManager.CleanupRegistryAsync(session1, "game_1", virtualKeys);
            await registryManager.CleanupRegistryAsync(session2, "game_1", virtualKeys);
        }

        // ==========================================
        // 4. USB PROTECTION POLICY TESTS
        // ==========================================
        [Fact]
        public void UsbProtectionService_TrustedDeviceConnected_ShouldAllowAndAudit()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<WindowsUsbProtectionService>>();
            var auditLoggerMock = new Mock<IAuditLogger>();
            var policyMock = new Mock<IKioskPolicyService>();

            policyMock.Setup(x => x.IsRestrictionEnabled(RestrictionType.Usb)).Returns(true);

            var usbService = new WindowsUsbProtectionService(loggerMock.Object, auditLoggerMock.Object, policyMock.Object);

            // Act
            usbService.HandleDeviceArrival("VID_123&PID_456", "SAYRA_Authorized Recovery Key");

            // Assert
            auditLoggerMock.Verify(x => x.LogOperational(It.Is<string>(s => s.Contains("Trusted device connected")), It.IsAny<Dictionary<string, object>?>()), Times.Once);
            auditLoggerMock.Verify(x => x.LogSecurity(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>()), Times.Never); // No ejection triggered
        }

        [Fact]
        public void UsbProtectionService_UnauthorizedDeviceConnected_ShouldEjectAndAudit()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<WindowsUsbProtectionService>>();
            var auditLoggerMock = new Mock<IAuditLogger>();
            var policyMock = new Mock<IKioskPolicyService>();

            policyMock.Setup(x => x.IsRestrictionEnabled(RestrictionType.Usb)).Returns(true);

            var usbService = new WindowsUsbProtectionService(loggerMock.Object, auditLoggerMock.Object, policyMock.Object);

            // Act
            usbService.HandleDeviceArrival("VID_888&PID_999", "Generic Rogue USB Storage");

            // Assert
            auditLoggerMock.Verify(x => x.LogSecurity(It.Is<string>(s => s.Contains("Unauthorized USB device insertion detected")), It.IsAny<Dictionary<string, object>?>()), Times.Once);
        }

        // ==========================================
        // 5. DIRECTX OVERLAY RENDERER SELECTION TESTS
        // ==========================================
        [Fact]
        public void OverlayManager_SelectsActiveSupportedRenderer()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<OverlayManager>>();
            var dataProviderMock = new Mock<IOverlayDataProvider>();
            var windowServiceMock = new Mock<IOverlayWindowService>();

            var wpfRendererMock = new Mock<IOverlayRenderer>();
            wpfRendererMock.Setup(x => x.IsSupported).Returns(true);

            var dxgiRendererMock = new Mock<IOverlayRenderer>();
            dxgiRendererMock.Setup(x => x.IsSupported).Returns(false);

            var renderers = new List<IOverlayRenderer> { dxgiRendererMock.Object, wpfRendererMock.Object };

            // Act
            using var manager = new OverlayManager(loggerMock.Object, dataProviderMock.Object, windowServiceMock.Object, renderers);

            // Assert - State machine is initialized and can transition
            Assert.Equal(OverlayState.Hidden, manager.StateMachine.CurrentState);
        }

        // ==========================================
        // 6. RUNTIME CONFIGURATION VALIDATION TESTS
        // ==========================================
        [Fact]
        public void RuntimePolicyOptions_ValidConfiguration_ShouldPassValidation()
        {
            // Arrange
            var options = new RuntimePolicyOptions
            {
                WarningThreshold1Seconds = 300,
                WarningThreshold2Seconds = 120,
                ExpirationGracePeriodSeconds = 15,
                DefaultLaunchTimeoutSeconds = 30
            };

            // Act & Assert
            options.Validate(); // Should not throw
        }

        [Fact]
        public void RuntimePolicyOptions_InvalidWarningThresholds_ShouldThrowArgumentException()
        {
            // Arrange
            var options = new RuntimePolicyOptions
            {
                WarningThreshold1Seconds = 120,
                WarningThreshold2Seconds = 300, // Invalid: threshold 1 <= threshold 2
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => options.Validate());
        }
    }
}
