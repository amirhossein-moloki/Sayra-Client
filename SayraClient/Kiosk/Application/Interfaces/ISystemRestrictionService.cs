namespace SayraClient.Kiosk.Application.Interfaces;

public interface ISystemRestrictionService
{
    void EnableSystemRestrictions();
    void DisableSystemRestrictions();
    bool IsSystemRestrictionActive();
    bool IsProcessBlocked(string processName);
}
