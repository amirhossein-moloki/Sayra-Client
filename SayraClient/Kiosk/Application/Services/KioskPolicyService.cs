using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using SayraClient.Kiosk.Application.Interfaces;
using SayraClient.Kiosk.Domain.Models;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Kiosk.Application.Services;

public class KioskPolicyService : IKioskPolicyService
{
    private readonly IAuditLogger _auditLogger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private KioskPolicy _currentPolicy;
    private readonly string _policyFilePath;

    public KioskPolicyService(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
        _policyFilePath = Path.Combine(AppContext.BaseDirectory, "kiosk_policy.json");
        _currentPolicy = LoadPolicyFromFile();
    }

    public KioskPolicy GetCurrentPolicy()
    {
        _lock.Wait();
        try
        {
            return _currentPolicy;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void UpdatePolicy(KioskPolicy policy)
    {
        _lock.Wait();
        try
        {
            _currentPolicy = policy;
            SavePolicyToFile(policy);
            _auditLogger.LogAudit("Kiosk policy updated and saved to file.");
        }
        finally
        {
            _lock.Release();
        }
    }

    public bool IsRestrictionEnabled(RestrictionType restrictionType)
    {
        _lock.Wait();
        try
        {
            return restrictionType switch
            {
                RestrictionType.Keyboard => _currentPolicy.EnableKeyboardRestriction,
                RestrictionType.Mouse => _currentPolicy.EnableMouseRestriction,
                RestrictionType.System => _currentPolicy.EnableSystemRestriction,
                RestrictionType.Usb => _currentPolicy.EnableUsbRestriction,
                _ => false
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public void ApplyPolicy(KioskPolicy policy)
    {
        _lock.Wait();
        try
        {
            _currentPolicy = policy;
            _auditLogger.LogAudit("Kiosk policy applied successfully.");
        }
        finally
        {
            _lock.Release();
        }
    }

    private KioskPolicy LoadPolicyFromFile()
    {
        try
        {
            if (File.Exists(_policyFilePath))
            {
                var json = File.ReadAllText(_policyFilePath);
                var policy = JsonSerializer.Deserialize<KioskPolicy>(json);
                if (policy != null) return policy;
            }
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperational($"Failed to load kiosk policy from file: {ex.Message}. Using defaults.");
        }

        // Return default policy
        return new KioskPolicy
        {
            EnableKeyboardRestriction = true,
            EnableMouseRestriction = true,
            EnableSystemRestriction = true,
            EnableUsbRestriction = true,
            MaintenanceModeAllowed = true
        };
    }

    private void SavePolicyToFile(KioskPolicy policy)
    {
        try
        {
            var json = JsonSerializer.Serialize(policy, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_policyFilePath, json);
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperational($"Failed to save kiosk policy to file: {ex.Message}");
        }
    }
}
