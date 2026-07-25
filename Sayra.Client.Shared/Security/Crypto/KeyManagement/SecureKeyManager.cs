using System;

namespace Sayra.Client.Shared.Security.Crypto.KeyManagement;

public class SecureKeyManager : IDisposable
{
    private readonly KeyLifecycleManager _lifecycleManager;
    private readonly KeyRotationService _rotationService;
    private readonly object _lock = new();
    private bool _isDisposed;

    public SecureKeyManager()
    {
        _lifecycleManager = new KeyLifecycleManager();
        _rotationService = new KeyRotationService(_lifecycleManager);
    }

    public void RotateKey()
    {
        lock (_lock)
        {
            CheckDisposed();
            _rotationService.RotateKeyManual();
        }
    }

    public void TriggerEmergencyRotation()
    {
        lock (_lock)
        {
            CheckDisposed();
            _rotationService.RotateKeyEmergency();
        }
    }

    public byte[]? GetCurrentSessionKey()
    {
        lock (_lock)
        {
            CheckDisposed();
            return _rotationService.ActiveKey?.GetSessionKeyBytes();
        }
    }

    public void SetManualSessionKey(byte[] rawKey)
    {
        if (rawKey == null) throw new ArgumentNullException(nameof(rawKey));
        if (rawKey.Length != 32) throw new ArgumentException("Session key must be 256 bits (32 bytes).", nameof(rawKey));

        lock (_lock)
        {
            CheckDisposed();
            TriggerEmergencyRotation();

            var provider = _rotationService.ActiveKey;
            if (provider != null)
            {
                provider.LoadSessionKey(rawKey);
            }
        }
    }

    private void CheckDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(SecureKeyManager));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            _rotationService.Dispose();
            _lifecycleManager.Dispose();
            _isDisposed = true;
        }
    }
}
