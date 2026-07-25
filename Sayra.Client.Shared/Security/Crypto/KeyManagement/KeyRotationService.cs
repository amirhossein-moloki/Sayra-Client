using System;

namespace Sayra.Client.Shared.Security.Crypto.KeyManagement;

public class KeyRotationService : IDisposable
{
    private readonly KeyLifecycleManager _lifecycleManager;
    private readonly TimeSpan _rotationInterval;
    private SessionKeyProvider? _activeKey;
    private readonly object _lock = new();
    private bool _isDisposed;

    public SessionKeyProvider? ActiveKey
    {
        get
        {
            lock (_lock)
            {
                CheckRotationNeeded();
                return _activeKey;
            }
        }
    }

    public KeyRotationService(KeyLifecycleManager lifecycleManager, TimeSpan? rotationInterval = null)
    {
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _rotationInterval = rotationInterval ?? TimeSpan.FromHours(1);
    }

    public void RotateKeyManual()
    {
        lock (_lock)
        {
            CheckDisposed();
            PerformRotation();
        }
    }

    public void RotateKeyEmergency()
    {
        lock (_lock)
        {
            CheckDisposed();
            if (_activeKey != null)
            {
                _activeKey.ForceExpire();
                _activeKey.DestroyKey();
                _activeKey.Dispose();
                _activeKey = null;
            }
            PerformRotation();
        }
    }

    private void CheckRotationNeeded()
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            if (_activeKey == null || _activeKey.IsExpired() || _activeKey.State == KeyState.Destroyed ||
                (DateTime.UtcNow - _activeKey.CreationTime) >= _rotationInterval)
            {
                PerformRotation();
            }
        }
    }

    private void PerformRotation()
    {
        lock (_lock)
        {
            var oldKey = _activeKey;

            // Create a new key gracefully
            _activeKey = _lifecycleManager.CreateKey();

            if (oldKey != null)
            {
                oldKey.ForceExpire();
                oldKey.DestroyKey();
                oldKey.Dispose();
            }

            _lifecycleManager.CleanupKeys();
        }
    }

    private void CheckDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(KeyRotationService));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            if (_activeKey != null)
            {
                _activeKey.DestroyKey();
                _activeKey.Dispose();
                _activeKey = null;
            }
            _isDisposed = true;
        }
    }
}
