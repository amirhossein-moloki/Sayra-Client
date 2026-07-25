using System;
using System.Runtime.InteropServices;
using SayraClient.Kiosk.Application.Interfaces;
using SayraClient.Kiosk.Domain.Models;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Kiosk.Application.Services;

public class MouseRestrictionService : IMouseRestrictionService
{
    private readonly IAuditLogger _auditLogger;
    private readonly IKioskPolicyService _policyService;
    private bool _isRestricted;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClipCursor(ref RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClipCursor(IntPtr lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClipCursor(out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    public MouseRestrictionService(IAuditLogger auditLogger, IKioskPolicyService policyService)
    {
        _auditLogger = auditLogger;
        _policyService = policyService;
    }

    public void EnableMouseRestriction(IntPtr? windowHandle = null)
    {
        if (!_policyService.IsRestrictionEnabled(RestrictionType.Mouse)) return;

        _isRestricted = true;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            RECT rect = new();
            if (windowHandle.HasValue && windowHandle.Value != IntPtr.Zero)
            {
                rect = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
                ClipCursor(ref rect);
            }
            else
            {
                rect = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
                ClipCursor(ref rect);
            }
        }
        else
        {
            _auditLogger.LogOperational("Mouse confinement simulated (non-Windows).");
        }

        _auditLogger.LogSecurity("[Kiosk Security] Mouse confinement restriction enabled.");
    }

    public void DisableMouseRestriction()
    {
        _isRestricted = false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ClipCursor(IntPtr.Zero);
        }

        _auditLogger.LogSecurity("[Kiosk Security] Mouse confinement restriction disabled.");
    }

    public bool IsMouseRestricted()
    {
        return _isRestricted;
    }
}
