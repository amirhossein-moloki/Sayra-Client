namespace SayraClient.Kiosk.Application.Interfaces;

public interface IKeyboardRestrictionService
{
    void EnableKeyboardRestrictions();
    void DisableKeyboardRestrictions();
    bool IsKeyboardHookActive();
}
