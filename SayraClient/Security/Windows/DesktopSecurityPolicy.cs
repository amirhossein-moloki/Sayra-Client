using System;
using System.Collections.Generic;

namespace SayraClient.Security.Windows;

public class DesktopSecurityPolicy
{
    public string SecureDesktopName { get; set; } = "SAYRA_SECURE_DESKTOP";

    public HashSet<string> ApprovedApplications { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "SayraClient",
        "Sayra.UI",
        "Sayra.Client.UI",
        "Sayra.Client.Guardian",
        "dwm",
        "conhost"
    };

    public HashSet<string> BlockedApplications { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "taskmgr",
        "cmd",
        "powershell",
        "powershell_ise",
        "bash",
        "regedit"
    };

    public bool IsShortcutBlocked(int virtualKeyCode, int modifiers)
    {
        bool isAlt = (modifiers & 1) != 0;
        bool isCtrl = (modifiers & 2) != 0;
        bool isShift = (modifiers & 4) != 0;

        // Windows Keys (LWin = 91, RWin = 92)
        if (virtualKeyCode == 91 || virtualKeyCode == 92)
        {
            return true;
        }

        // Alt + Tab (Tab = 9)
        if (virtualKeyCode == 9 && isAlt)
        {
            return true;
        }

        // Ctrl + Esc, Alt + Esc (Esc = 27)
        if (virtualKeyCode == 27 && (isCtrl || isAlt))
        {
            return true;
        }

        // Alt + F4 (F4 = 115)
        if (virtualKeyCode == 115 && isAlt)
        {
            return true;
        }

        // Ctrl + Shift + Esc
        if (virtualKeyCode == 27 && isCtrl && isShift)
        {
            return true;
        }

        return false;
    }
}
