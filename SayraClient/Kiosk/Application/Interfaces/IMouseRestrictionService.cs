using System;

namespace SayraClient.Kiosk.Application.Interfaces;

public interface IMouseRestrictionService
{
    void EnableMouseRestriction(IntPtr? windowHandle = null);
    void DisableMouseRestriction();
    bool IsMouseRestricted();
}
