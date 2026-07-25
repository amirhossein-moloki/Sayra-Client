using System;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Security.Crypto.KeyManagement;

namespace SayraClient.Services;

public class SessionKeyManager : IDisposable
{
    private readonly SecureKeyManager _secureKeyManager;
    private readonly object _lock = new();

    public SessionKeyManager()
    {
        _secureKeyManager = new SecureKeyManager();
    }

    public SessionKeyManager(ILogger<SessionKeyManager> logger)
    {
        _secureKeyManager = new SecureKeyManager();
    }

    public void SetSessionKey(byte[] key)
    {
        lock (_lock)
        {
            _secureKeyManager.SetManualSessionKey(key);
        }
    }

    public byte[]? GetSessionKey()
    {
        lock (_lock)
        {
            return _secureKeyManager.GetCurrentSessionKey();
        }
    }

    public void ClearSessionKey()
    {
        lock (_lock)
        {
            _secureKeyManager.TriggerEmergencyRotation();
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            lock (_lock)
            {
                byte[]? key = _secureKeyManager.GetCurrentSessionKey();
                if (key != null)
                {
                    // Zero the temporary copy immediately
                    Array.Clear(key, 0, key.Length);
                    return true;
                }
                return false;
            }
        }
    }

    public void Dispose()
    {
        _secureKeyManager.Dispose();
    }
}
