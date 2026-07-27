using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Diagnostics.Telemetry
{
    public class SessionTelemetryCollector : ITelemetryCollector
    {
        private readonly ILogger<SessionTelemetryCollector> _logger;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public SessionTelemetryCollector(ILogger<SessionTelemetryCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task CollectAsync(LiveTelemetryData data, CancellationToken cancellationToken = default)
        {
            try
            {
                data.LoggedUser = GetLoggedUser();
                data.WindowsSessionId = Process.GetCurrentProcess().SessionId;
                data.ActiveProcess = GetActiveProcessName();
                data.KioskState = GetKioskState();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect Session telemetry.");
                data.LoggedUser = "Unknown";
                data.WindowsSessionId = -1;
                data.ActiveProcess = "Unknown";
                data.KioskState = "Unknown";
            }
            return Task.CompletedTask;
        }

        private string GetLoggedUser()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using (var identity = WindowsIdentity.GetCurrent())
                    {
                        return identity?.Name ?? Environment.UserName;
                    }
                }
                return Environment.UserName;
            }
            catch { return "Unknown"; }
        }

        private string GetActiveProcessName()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "dotnet";
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid > 0)
                    {
                        using (var proc = Process.GetProcessById((int)pid))
                        {
                            return proc.ProcessName;
                        }
                    }
                }
            }
            catch { }
            return "explorer";
        }

        private string GetKioskState()
        {
            return "Active";
        }
    }
}
