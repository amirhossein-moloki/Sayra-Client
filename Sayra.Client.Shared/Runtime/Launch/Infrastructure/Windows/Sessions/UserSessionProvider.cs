using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Launch.Domain.Exceptions;

namespace Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Sessions
{
    public class UserSessionProvider : IUserSessionProvider
    {
        private readonly ILogger<UserSessionProvider> _logger;

        public UserSessionProvider(ILogger<UserSessionProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<UserSessionInfo> GetActiveSessionAsync()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogInformation("[Non-Windows] Returning active user session stub.");
                return Task.FromResult(new UserSessionInfo
                {
                    SessionId = 1,
                    Username = "RestrictedUser",
                    Domain = "WORKGROUP",
                    UserSid = "S-1-5-21-mock-interactive-sid",
                    IsInteractive = true
                });
            }

            try
            {
                uint sessionId = WTSGetActiveConsoleSessionId();
                if (sessionId == 0xFFFFFFFF) // INVALID_SESSION_ID
                {
                    _logger.LogWarning("No active console session found.");
                    throw new UserSessionUnavailableException("No active interactive console session available.");
                }

                string username = QuerySessionInfo(sessionId, WTS_INFO_CLASS.WTSUserName);
                string domain = QuerySessionInfo(sessionId, WTS_INFO_CLASS.WTSDomainName);

                _logger.LogInformation("Active console session detected. Session ID: {SessionId}, User: {Domain}\\{Username}", sessionId, domain, username);

                return Task.FromResult(new UserSessionInfo
                {
                    SessionId = sessionId,
                    Username = username,
                    Domain = domain,
                    UserSid = "S-1-5-21-mock-retrieved-sid",
                    IsInteractive = true
                });
            }
            catch (Exception ex) when (!(ex is UserSessionUnavailableException))
            {
                _logger.LogError(ex, "Failed to retrieve active user session details via WTS.");
                throw new UserSessionUnavailableException("Failed to discover active Windows interactive session.", ex);
            }
        }

        private string QuerySessionInfo(uint sessionId, WTS_INFO_CLASS infoClass)
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, infoClass, out buffer, out uint bytesReturned))
                {
                    if (bytesReturned > 0 && buffer != IntPtr.Zero)
                    {
                        return Marshal.PtrToStringUni(buffer) ?? string.Empty;
                    }
                }
                return string.Empty;
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    WTSFreeMemory(buffer);
                }
            }
        }

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool WTSQuerySessionInformation(
            IntPtr hServer,
            uint sessionId,
            WTS_INFO_CLASS wtsInfoClass,
            out IntPtr ppBuffer,
            out uint pBytesReturned);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        private enum WTS_INFO_CLASS
        {
            WTSUserName = 5,
            WTSDomainName = 7
        }
    }
}
