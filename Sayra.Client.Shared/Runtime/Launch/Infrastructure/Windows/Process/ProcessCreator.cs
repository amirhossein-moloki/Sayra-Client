using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Launch.Domain.Models;
using Sayra.Client.Shared.Runtime.Launch.Domain.Exceptions;

namespace Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Process
{
    public class ProcessCreator : IProcessCreator
    {
        private readonly ILogger<ProcessCreator> _logger;
        private readonly IUserTokenService _tokenService;

        public ProcessCreator(ILogger<ProcessCreator> logger, IUserTokenService tokenService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        }

        public async Task<LaunchResult> CreateProcessAsync(LaunchRequest request, LaunchProfile profile, uint sessionId)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogInformation("[Non-Windows] Performing cross-platform process creation fallback.");
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = request.ExecutablePath,
                        Arguments = request.Arguments,
                        WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                            ? Path.GetDirectoryName(request.ExecutablePath)
                            : request.WorkingDirectory,
                        UseShellExecute = false
                    };

                    _logger.LogInformation("[Non-Windows Fallback] Starting process '{Path}'", psi.FileName);
                    var sysProcess = System.Diagnostics.Process.Start(psi);
                    if (sysProcess == null)
                    {
                        return new LaunchResult
                        {
                            Success = false,
                            ErrorMessage = "[Non-Windows Fallback] Failed to launch process."
                        };
                    }

                    return new LaunchResult
                    {
                        Success = true,
                        ProcessId = sysProcess.Id
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Non-Windows Fallback] Failed to start process.");
                    return new LaunchResult
                    {
                        Success = false,
                        ErrorMessage = $"[Non-Windows Fallback] Exception: {ex.Message}"
                    };
                }
            }

            IntPtr hToken = IntPtr.Zero;
            IntPtr lpEnv = IntPtr.Zero;
            try
            {
                hToken = await _tokenService.GetUserTokenAsync(sessionId);
                _logger.LogInformation("Creating environment block for user session token.");
                if (!CreateEnvironmentBlock(out lpEnv, hToken, false))
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    _logger.LogWarning("CreateEnvironmentBlock failed. ErrorCode: {ErrorCode}. Falling back to default system environment.", errorCode);
                    lpEnv = IntPtr.Zero;
                }

                var si = new STARTUPINFO();
                si.cb = Marshal.SizeOf(si);
                si.lpDesktop = @"winsta0\default"; // Route to the interactive window station and desktop

                var pi = new PROCESS_INFORMATION();

                uint creationFlags = 0;
                if (lpEnv != IntPtr.Zero)
                {
                    const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
                    creationFlags |= CREATE_UNICODE_ENVIRONMENT;
                }

                string workingDir = string.IsNullOrWhiteSpace(profile.WorkingDirectory)
                    ? Path.GetDirectoryName(request.ExecutablePath) ?? string.Empty
                    : profile.WorkingDirectory;

                string commandLine = $"\"{request.ExecutablePath}\" {request.Arguments}".Trim();

                _logger.LogInformation("Spawning Process: '{CmdLine}' in directory '{Dir}' inside interactive Session {SessionId}", commandLine, workingDir, sessionId);

                bool success = CreateProcessAsUser(
                    hToken,
                    null, // lpApplicationName
                    commandLine,
                    IntPtr.Zero, // lpProcessAttributes
                    IntPtr.Zero, // lpThreadAttributes
                    false, // bInheritHandles
                    creationFlags,
                    lpEnv,
                    string.IsNullOrWhiteSpace(workingDir) ? null : workingDir,
                    ref si,
                    out pi);

                if (!success)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    _logger.LogError("CreateProcessAsUser failed with error code: {ErrorCode}", errorCode);
                    throw new ProcessCreationException($"CreateProcessAsUser failed. Win32 Error: {errorCode}");
                }

                _logger.LogInformation("Successfully spawned game process under interactive session. PID: {Pid}, ThreadID: {Tid}", pi.dwProcessId, pi.dwThreadId);

                // Clean up process & thread handles immediately to avoid handle leaks
                if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);

                return new LaunchResult
                {
                    Success = true,
                    ProcessId = pi.dwProcessId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to launch game process via CreateProcessAsUser.");
                throw new ProcessCreationException("Failed to launch game process under interactive session token.", ex);
            }
            finally
            {
                if (lpEnv != IntPtr.Zero)
                {
                    DestroyEnvironmentBlock(lpEnv);
                }
                if (hToken != IntPtr.Zero)
                {
                    await _tokenService.ReleaseTokenAsync(hToken);
                }
            }
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string? lpApplicationName,
            string? lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }
    }
}
