using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Security;

namespace SayraClient.Security.Windows;

public class DesktopSessionManager : IDisposable
{
    private readonly ILogger<DesktopSessionManager> _logger;
    private readonly SecureDesktopManager _desktopManager;
    private readonly DesktopSecurityPolicy _securityPolicy;
    private readonly IIntegrityValidator _integrityValidator;
    private readonly object _lock = new();
    private Process? _shellProcess;
    private CancellationTokenSource? _monitorCts;
    private bool _isRunning;
    private string? _shellPath;
    private string? _shellArgs;
    private IntPtr _userToken;
    private Action? _repairCallback;

    public DesktopSessionManager(
        ILogger<DesktopSessionManager> logger,
        SecureDesktopManager desktopManager,
        DesktopSecurityPolicy securityPolicy,
        IIntegrityValidator integrityValidator)
    {
        _logger = logger;
        _desktopManager = desktopManager;
        _securityPolicy = securityPolicy;
        _integrityValidator = integrityValidator;
    }

    public bool IsRunning => _isRunning;

    public bool StartSession(string shellExecutablePath, string arguments, IntPtr userToken, Action repairCallback)
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                _logger.LogWarning("Desktop session is already running.");
                return true;
            }

            _logger.LogInformation("Starting Secure Desktop Session...");
            _shellPath = shellExecutablePath;
            _shellArgs = arguments;
            _userToken = userToken;
            _repairCallback = repairCallback;

            if (!_desktopManager.CreateSecureDesktop())
            {
                _logger.LogError("Failed to create secure desktop.");
                return false;
            }

            if (!_desktopManager.SwitchToSecureDesktop())
            {
                _logger.LogError("Failed to switch to secure desktop.");
                return false;
            }

            if (!SpawnShellProcess())
            {
                _logger.LogError("Failed to launch shell process inside secure desktop.");
                return false;
            }

            _isRunning = true;
            StartSelfHealingMonitor();

            return true;
        }
    }

    private bool SpawnShellProcess()
    {
        if (string.IsNullOrEmpty(_shellPath)) return false;

        if (File.Exists(_shellPath) || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            bool spawned = _desktopManager.LaunchProcessInSecureDesktop(_shellPath, _shellArgs ?? string.Empty, _userToken, out int spawnedPid);
            if (!spawned)
            {
                return false;
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _shellProcess = Process.GetProcessById(spawnedPid);
                }
                else
                {
                    // Simulated process for testing
                    _shellProcess = Process.GetCurrentProcess();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve Process object for PID {Pid}.", spawnedPid);
            }

            return true;
        }
        else
        {
            _logger.LogWarning("Shell executable path not found: {Path}", _shellPath);
            return false;
        }
    }

    public void StopSession()
    {
        lock (_lock)
        {
            if (!_isRunning) return;

            _logger.LogInformation("Stopping Secure Desktop Session...");
            StopSelfHealingMonitor();

            try
            {
                if (_shellProcess != null && !_shellProcess.HasExited)
                {
                    _shellProcess.Kill(true);
                    _shellProcess = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to terminate shell process during teardown.");
            }

            _desktopManager.SwitchToDefaultDesktop();
            _desktopManager.DestroySecureDesktop();
            _isRunning = false;
        }
    }

    private void StartSelfHealingMonitor()
    {
        _monitorCts = new CancellationTokenSource();
        var token = _monitorCts.Token;

        Task.Run(async () =>
        {
            _logger.LogInformation("Self-healing monitoring loop started.");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, token);

                    lock (_lock)
                    {
                        if (!_isRunning) break;

                        // 1. Monitor Shell Process (Relaunch if exited or crashed)
                        if (_shellProcess == null || _shellProcess.HasExited)
                        {
                            _logger.LogWarning("Self-healing: Shell process terminated unexpectedly. Relaunching shell inside secure desktop...");
                            SpawnShellProcess();
                        }

                        // 2. Repair policies (Registry lockdowns and Keyboard Hook checks) via decoupled callback
                        try
                        {
                            _repairCallback?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to invoke repair callback in monitoring loop.");
                        }

                        // 3. Monitor unauthorized processes with whitelisting & existing IntegrityValidator
                        var processes = Process.GetProcesses();
                        foreach (var proc in processes)
                        {
                            try
                            {
                                string name = proc.ProcessName;
                                if (_securityPolicy.BlockedApplications.Contains(name))
                                {
                                    _logger.LogWarning("Self-healing: Blocked process '{Name}' (PID: {Pid}) detected. Terminating.", name, proc.Id);
                                    proc.Kill(true);
                                    continue;
                                }

                                // If not an approved application or system whitelist, perform integrity validation
                                if (!_securityPolicy.ApprovedApplications.Contains(name))
                                {
                                    // Integrate with existing IIntegrityValidator
                                    bool isValid = _integrityValidator.ValidateProcess(proc.Id);
                                    if (!isValid)
                                    {
                                        _logger.LogWarning("Self-healing: Integrity check failed for process '{Name}' (PID: {Pid}). Terminating.", name, proc.Id);
                                        proc.Kill(true);
                                    }
                                }
                            }
                            catch
                            {
                                // Handled safely (e.g. for processes we don't have access to)
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in self-healing monitoring loop.");
                }
            }
            _logger.LogInformation("Self-healing monitoring loop stopped.");
        }, token);
    }

    private void StopSelfHealingMonitor()
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;
    }

    public void Dispose()
    {
        StopSession();
    }
}
