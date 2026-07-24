using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Sayra.UI.Notifications.Services;

public interface IWindowsNotificationChannel
{
    void ShowNotification(string title, string body);
}

public class WindowsNotificationChannel : IWindowsNotificationChannel
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uVersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_INFO = 0x00000010;
    private const int NIF_TIP = 0x00000004;
    private const int NIIF_INFO = 0x00000001;

    public void ShowNotification(string title, string body)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var nid = new NOTIFYICONDATA();
            nid.cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA));
            nid.hWnd = IntPtr.Zero;
            nid.uID = 1001;
            nid.uFlags = NIF_INFO | NIF_TIP; // Removed NIF_ICON to avoid handle mismatch and unstable Shell behavior
            nid.uVersionOrTimeout = 10000;
            nid.szTip = "SAYRA Enterprise Client";
            nid.szInfo = body;
            nid.szInfoTitle = title;
            nid.dwInfoFlags = NIIF_INFO;

            // Add the icon to system tray and display balloon tip
            Shell_NotifyIcon(NIM_ADD, ref nid);
            Shell_NotifyIcon(NIM_MODIFY, ref nid);

            // Gracefully remove the icon after 12 seconds
            Task.Run(async () =>
            {
                await Task.Delay(12000);
                Shell_NotifyIcon(NIM_DELETE, ref nid);
            });
        }
        catch
        {
            // Fail silently on native api errors
        }
    }
}
