using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using SayraClient.Kiosk.Application.Interfaces;
using SayraClient.Kiosk.Application.Services;
using SayraClient.Kiosk.Domain.Events;
using SayraClient.Kiosk.Domain.Models;
using SayraClient.Kiosk.Infrastructure.DeviceMonitoring;
using SayraClient.Kiosk.Infrastructure.Shell;
using SayraClient.Kiosk.Infrastructure.WindowsHooks;
using Sayra.Client.Shared.Interfaces;

namespace Sayra.Client.Configuration.Tests;

public class KioskSecurityTests
{
    private readonly Mock<IAuditLogger> _mockAuditLogger;

    public KioskSecurityTests()
    {
        _mockAuditLogger = new Mock<IAuditLogger>();
    }

    [Fact]
    public void PolicyEngine_LoadAndSave_AppliesCorrectly()
    {
        // Arrange
        var policyService = new KioskPolicyService(_mockAuditLogger.Object);
        var initialPolicy = policyService.GetCurrentPolicy();

        // Act
        var customPolicy = new KioskPolicy
        {
            EnableKeyboardRestriction = false,
            EnableMouseRestriction = true,
            EnableSystemRestriction = false,
            EnableUsbRestriction = true,
            MaintenanceModeAllowed = false
        };
        policyService.UpdatePolicy(customPolicy);

        // Assert
        var updatedPolicy = policyService.GetCurrentPolicy();
        Assert.False(updatedPolicy.EnableKeyboardRestriction);
        Assert.True(updatedPolicy.EnableMouseRestriction);
        Assert.False(updatedPolicy.EnableSystemRestriction);
        Assert.True(updatedPolicy.EnableUsbRestriction);
        Assert.False(updatedPolicy.MaintenanceModeAllowed);

        // Clean up or restore defaults
        policyService.UpdatePolicy(initialPolicy);
    }

    [Fact]
    public void KeyboardRestriction_EnableAndDisable_SetsHookActiveState()
    {
        // Arrange
        var mockPolicyService = new Mock<IKioskPolicyService>();
        mockPolicyService.Setup(p => p.IsRestrictionEnabled(RestrictionType.Keyboard)).Returns(true);

        using var keyboardService = new KeyboardRestrictionService(_mockAuditLogger.Object, mockPolicyService.Object);

        // Act
        keyboardService.EnableKeyboardRestrictions();

        // Assert
        Assert.True(keyboardService.IsKeyboardHookActive());

        // Act
        keyboardService.DisableKeyboardRestrictions();

        // Assert
        Assert.False(keyboardService.IsKeyboardHookActive());
    }

    [Fact]
    public void MouseRestriction_EnableAndDisable_SetsRestrictedState()
    {
        // Arrange
        var mockPolicyService = new Mock<IKioskPolicyService>();
        mockPolicyService.Setup(p => p.IsRestrictionEnabled(RestrictionType.Mouse)).Returns(true);

        var mouseService = new MouseRestrictionService(_mockAuditLogger.Object, mockPolicyService.Object);

        // Act
        mouseService.EnableMouseRestriction();

        // Assert
        Assert.True(mouseService.IsMouseRestricted());

        // Act
        mouseService.DisableMouseRestriction();

        // Assert
        Assert.False(mouseService.IsMouseRestricted());
    }

    [Fact]
    public async Task MaintenanceMode_AuthenticationAndTimeout_BehavesCorrectly()
    {
        // Arrange
        var mockPolicyService = new Mock<IKioskPolicyService>();
        var mockKeyboard = new Mock<IKeyboardRestrictionService>();
        var mockMouse = new Mock<IMouseRestrictionService>();
        var mockShell = new Mock<IShellProtectionService>();
        var mockSystem = new Mock<ISystemRestrictionService>();

        using var maintenanceService = new MaintenanceModeService(
            _mockAuditLogger.Object,
            mockPolicyService.Object,
            mockKeyboard.Object,
            mockMouse.Object,
            mockShell.Object,
            mockSystem.Object);

        // Set short timeout for testing
        maintenanceService.SetMaintenanceTimeout(TimeSpan.FromMilliseconds(200));

        // Act - Incorrect password
        bool wrongAuth = await maintenanceService.EnterMaintenanceModeAsync("WrongPassword!");

        // Assert
        Assert.False(wrongAuth);
        Assert.False(maintenanceService.IsMaintenanceModeActive());

        // Act - Correct password
        bool correctAuth = await maintenanceService.EnterMaintenanceModeAsync("Admin123!");

        // Assert
        Assert.True(correctAuth);
        Assert.True(maintenanceService.IsMaintenanceModeActive());

        // Verify restrictions were disabled
        mockKeyboard.Verify(k => k.DisableKeyboardRestrictions(), Times.Once);
        mockMouse.Verify(m => m.DisableMouseRestriction(), Times.Once);
        mockSystem.Verify(s => s.DisableSystemRestrictions(), Times.Once);
        mockShell.Verify(sh => sh.RestoreExplorerShell(), Times.Once);

        // Act - Register activity tick to extend life
        maintenanceService.RegisterActivityTick();

        // Wait for timeout relock
        await Task.Delay(500);

        // Assert - Relock should have automatically triggered
        Assert.False(maintenanceService.IsMaintenanceModeActive());
        mockKeyboard.Verify(k => k.EnableKeyboardRestrictions(), Times.Once);
        mockMouse.Verify(m => m.EnableMouseRestriction(null), Times.Once);
        mockSystem.Verify(s => s.EnableSystemRestrictions(), Times.Once);
        mockShell.Verify(sh => sh.RestoreSayraShell(), Times.Once);
    }

    [Fact]
    public void DeviceMonitoring_DeviceEvents_FiresCorrectly()
    {
        // Arrange
        var mockPolicyService = new Mock<IKioskPolicyService>();
        mockPolicyService.Setup(p => p.IsRestrictionEnabled(RestrictionType.Usb)).Returns(true);

        var deviceService = new DeviceControlService(_mockAuditLogger.Object, mockPolicyService.Object);
        deviceService.StartMonitoring();

        DeviceConnectedEvent? connectedEvent = null;
        UnauthorizedDeviceDetectedEvent? unauthorizedEvent = null;
        DeviceRemovedEvent? removedEvent = null;

        deviceService.DeviceConnected += ev => connectedEvent = ev;
        deviceService.UnauthorizedDeviceDetected += ev => unauthorizedEvent = ev;
        deviceService.DeviceRemoved += ev => removedEvent = ev;

        // Act - Simulate device insertion (DBT_DEVICEARRIVAL = 0x8000)
        IntPtr wParamArrival = (IntPtr)0x8000;
        IntPtr lParamGeneric = IntPtr.Zero;
        deviceService.HandleDeviceNotification(wParamArrival, lParamGeneric);

        // Assert
        Assert.NotNull(connectedEvent);
        Assert.NotNull(unauthorizedEvent);
        Assert.Equal("Generic USB Device", connectedEvent.DeviceName);
        Assert.Equal("Generic USB Device", unauthorizedEvent.DeviceName);

        // Act - Simulate device removal (DBT_DEVICEREMOVECOMPLETE = 0x8004)
        IntPtr wParamRemoval = (IntPtr)0x8004;
        deviceService.HandleDeviceNotification(wParamRemoval, lParamGeneric);

        // Assert
        Assert.NotNull(removedEvent);
        Assert.Equal("Generic USB Device", removedEvent.DeviceName);

        deviceService.StopMonitoring();
    }

    [Fact]
    public void SystemRestriction_ProcessBlocking_DetectsBlockedName()
    {
        // Arrange
        var mockPolicyService = new Mock<IKioskPolicyService>();
        using var systemService = new SystemRestrictionService(_mockAuditLogger.Object, mockPolicyService.Object);

        // Act & Assert
        Assert.True(systemService.IsProcessBlocked("taskmgr.exe"));
        Assert.True(systemService.IsProcessBlocked("cmd"));
        Assert.True(systemService.IsProcessBlocked("powershell.exe"));
        Assert.True(systemService.IsProcessBlocked("regedit"));
        Assert.False(systemService.IsProcessBlocked("Sayra.UI.exe"));
    }
}
