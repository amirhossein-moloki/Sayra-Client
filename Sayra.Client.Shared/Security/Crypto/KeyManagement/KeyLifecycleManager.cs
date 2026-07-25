using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Security.Crypto.KeyManagement;

public class KeyLifecycleManager : IDisposable
{
    private readonly List<SessionKeyProvider> _managedKeys = new();
    private readonly object _lock = new();
    private bool _isDisposed;

    public SessionKeyProvider CreateKey()
    {
        lock (_lock)
        {
            CheckDisposed();
            CleanupKeys();

            var provider = new SessionKeyProvider();
            provider.GenerateSessionKey();
            _managedKeys.Add(provider);
            return provider;
        }
    }

    public void CleanupKeys()
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            for (int i = _managedKeys.Count - 1; i >= 0; i--)
            {
                var key = _managedKeys[i];
                if (key.IsExpired() || key.State == KeyState.Destroyed)
                {
                    key.DestroyKey();
                    key.Dispose();
                    _managedKeys.RemoveAt(i);
                }
            }
        }
    }

    public void DestroyAll()
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            foreach (var key in _managedKeys)
            {
                key.DestroyKey();
                key.Dispose();
            }
            _managedKeys.Clear();
        }
    }

    private void CheckDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(KeyLifecycleManager));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            DestroyAll();
            _isDisposed = true;
        }
    }
}
