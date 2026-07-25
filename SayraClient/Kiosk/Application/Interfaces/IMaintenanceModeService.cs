using System;
using System.Threading.Tasks;

namespace SayraClient.Kiosk.Application.Interfaces;

public interface IMaintenanceModeService
{
    Task<bool> EnterMaintenanceModeAsync(string password);
    void ExitMaintenanceMode();
    bool IsMaintenanceModeActive();
    void SetMaintenanceTimeout(TimeSpan timeout);
    void RegisterActivityTick();
}
