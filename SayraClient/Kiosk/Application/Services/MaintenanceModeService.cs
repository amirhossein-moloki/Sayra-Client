using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SayraClient.Kiosk.Application.Interfaces;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Kiosk.Application.Services;

public class MaintenanceModeService : IMaintenanceModeService, IDisposable
{
    private readonly IAuditLogger _auditLogger;
    private readonly IKioskPolicyService _policyService;
    private readonly IKeyboardRestrictionService _keyboardService;
    private readonly IMouseRestrictionService _mouseService;
    private readonly IShellProtectionService _shellService;
    private readonly ISystemRestrictionService _systemService;

    private bool _isActive;
    private TimeSpan _timeout = TimeSpan.FromMinutes(20);
    private DateTime _lastActivityTime;
    private CancellationTokenSource? _timerCts;

    private readonly string _storedSaltHex;
    private readonly string _storedHashHex;

    public MaintenanceModeService(
        IAuditLogger auditLogger,
        IKioskPolicyService policyService,
        IKeyboardRestrictionService keyboardService,
        IMouseRestrictionService mouseService,
        IShellProtectionService shellService,
        ISystemRestrictionService systemService)
    {
        _auditLogger = auditLogger;
        _policyService = policyService;
        _keyboardService = keyboardService;
        _mouseService = mouseService;
        _shellService = shellService;
        _systemService = systemService;

        byte[] salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }
        _storedSaltHex = Convert.ToHexString(salt);
        _storedHashHex = HashPasswordWithPbkdf2("Admin123!", salt);
    }

    public Task<bool> EnterMaintenanceModeAsync(string password)
    {
        byte[] salt = Convert.FromHexString(_storedSaltHex);
        string challengeHash = HashPasswordWithPbkdf2(password, salt);

        if (challengeHash == _storedHashHex)
        {
            _isActive = true;
            _lastActivityTime = DateTime.UtcNow;

            _keyboardService.DisableKeyboardRestrictions();
            _mouseService.DisableMouseRestriction();
            _systemService.DisableSystemRestrictions();
            _shellService.RestoreExplorerShell();

            _auditLogger.LogAudit("[Kiosk Security] Administrator authenticated successfully. Entered Maintenance Mode.");

            _timerCts?.Cancel();
            _timerCts = new CancellationTokenSource();
            Task.Run(() => MonitorTimeoutAsync(_timerCts.Token));

            return Task.FromResult(true);
        }

        _auditLogger.LogSecurity("[Kiosk Security] Administrator authentication challenge failed: Invalid credentials.");
        return Task.FromResult(false);
    }

    public void ExitMaintenanceMode()
    {
        if (!_isActive) return;
        _isActive = false;

        _timerCts?.Cancel();
        _timerCts = null;

        _keyboardService.EnableKeyboardRestrictions();
        _mouseService.EnableMouseRestriction();
        _systemService.EnableSystemRestrictions();
        _shellService.RestoreSayraShell();

        _auditLogger.LogAudit("[Kiosk Security] Exited Maintenance Mode. Kiosk protection restored.");
    }

    public bool IsMaintenanceModeActive() => _isActive;

    public void SetMaintenanceTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        _auditLogger.LogOperational($"Maintenance timeout threshold configured to {_timeout.TotalMinutes} minutes.");
    }

    public void RegisterActivityTick()
    {
        _lastActivityTime = DateTime.UtcNow;
        _auditLogger.LogOperational("Administrator activity tick registered.");
    }

    private async Task MonitorTimeoutAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var elapsed = DateTime.UtcNow - _lastActivityTime;
                if (elapsed >= _timeout)
                {
                    _auditLogger.LogSecurity("[Kiosk Security] Maintenance session idle timeout reached. Automatically relocking workstation.");
                    ExitMaintenanceMode();
                    break;
                }
            }
            catch (Exception ex)
            {
                _auditLogger.LogOperational($"Error in maintenance timer: {ex.Message}");
            }

            try
            {
                await Task.Delay(50, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private string HashPasswordWithPbkdf2(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);
        return Convert.ToHexString(hash);
    }

    public void Dispose()
    {
        _timerCts?.Cancel();
    }
}
