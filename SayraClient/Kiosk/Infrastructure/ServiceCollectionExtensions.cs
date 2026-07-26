using Microsoft.Extensions.DependencyInjection;
using SayraClient.Kiosk.Application.Interfaces;
using SayraClient.Kiosk.Application.Services;
using SayraClient.Kiosk.Infrastructure.DeviceMonitoring;
using SayraClient.Kiosk.Infrastructure.Shell;
using SayraClient.Kiosk.Infrastructure.WindowsHooks;

namespace SayraClient.Kiosk.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKioskSecurityServices(this IServiceCollection services)
    {
        services.AddSingleton<IKioskPolicyService, KioskPolicyService>();
        services.AddSingleton<IKeyboardRestrictionService, KeyboardRestrictionService>();
        services.AddSingleton<IMouseRestrictionService, MouseRestrictionService>();
        services.AddSingleton<IShellProtectionService, ShellProtectionService>();
        services.AddSingleton<ISystemRestrictionService, SystemRestrictionService>();
        services.AddSingleton<IUsbProtectionService, WindowsUsbProtectionService>();
        services.AddSingleton<IDeviceControlService, DeviceControlService>();
        services.AddSingleton<IMaintenanceModeService, MaintenanceModeService>();

        return services;
    }
}
