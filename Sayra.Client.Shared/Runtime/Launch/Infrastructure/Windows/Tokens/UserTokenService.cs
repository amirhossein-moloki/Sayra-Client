using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Launch.Domain.Exceptions;

namespace Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Tokens
{
    public class UserTokenService : IUserTokenService
    {
        private readonly ILogger<UserTokenService> _logger;

        public UserTokenService(ILogger<UserTokenService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<IntPtr> GetUserTokenAsync(uint sessionId)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogInformation("[Non-Windows] GetUserTokenAsync returning IntPtr.Zero fallback token.");
                return Task.FromResult(IntPtr.Zero);
            }

            _logger.LogInformation("Querying user token for WTS Session: {SessionId}", sessionId);
            if (!WTSQueryUserToken(sessionId, out IntPtr hToken))
            {
                int errorCode = Marshal.GetLastWin32Error();
                _logger.LogError("WTSQueryUserToken failed with error code: {ErrorCode}", errorCode);
                throw new TokenCreationException($"WTSQueryUserToken failed for Session ID {sessionId}. LastError: {errorCode}");
            }

            using var primaryToken = new SafeTokenHandle(hToken);

            _logger.LogInformation("Duplicating session user token as a primary token.");
            const uint TOKEN_ALL_ACCESS = 0xF01FF;
            if (!DuplicateTokenEx(
                primaryToken.DangerousGetHandle(),
                TOKEN_ALL_ACCESS,
                IntPtr.Zero,
                SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                TOKEN_TYPE.TokenPrimary,
                out IntPtr hDuplicateToken))
            {
                int errorCode = Marshal.GetLastWin32Error();
                _logger.LogError("DuplicateTokenEx failed with error code: {ErrorCode}", errorCode);
                throw new TokenCreationException($"Failed to duplicate interactive user session token. LastError: {errorCode}");
            }

            _logger.LogInformation("Interactive primary token successfully created.");
            return Task.FromResult(hDuplicateToken);
        }

        public Task<bool> ValidateTokenAsync(IntPtr hToken)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogInformation("[Non-Windows] ValidateTokenAsync returning true.");
                return Task.FromResult(true);
            }

            if (hToken == IntPtr.Zero || hToken == new IntPtr(-1))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public Task ReleaseTokenAsync(IntPtr hToken)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Task.CompletedTask;
            }

            if (hToken != IntPtr.Zero && hToken != new IntPtr(-1))
            {
                _logger.LogInformation("Releasing duplicate user token handle.");
                CloseHandle(hToken);
            }
            return Task.CompletedTask;
        }

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private extern static bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            IntPtr lpTokenAttributes,
            SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
            TOKEN_TYPE TokenType,
            out IntPtr phNewToken);

        private enum SECURITY_IMPERSONATION_LEVEL
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation
        }

        private enum TOKEN_TYPE
        {
            TokenPrimary = 1,
            TokenImpersonation
        }
    }
}
