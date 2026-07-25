using System;
using Sayra.Client.Shared.Security.Memory;

namespace Sayra.Client.Shared.Security.Crypto.KeyManagement;

public class SessionKeyProvider : IDisposable
{
    private SecureMemoryBuffer? _protectedKey;
    private DateTime _creationTime;
    private DateTime _expirationTime;
    private readonly TimeSpan _lifetime = TimeSpan.FromHours(1); // Default session key lifetime of 1 hour
    private readonly object _lock = new();
    private KeyState _state = KeyState.Created;

    public KeyState State
    {
        get { lock (_lock) return _state; }
        private set { lock (_lock) _state = value; }
    }

    public DateTime ExpirationTime => _expirationTime;
    public DateTime CreationTime => _creationTime;

    public void GenerateSessionKey()
    {
        lock (_lock)
        {
            DestroyKey();

            // Session keys must be multiple of 16 for MemoryProtector.Protect
            // 256-bit AES key is 32 bytes (which is multiple of 16!)
            _protectedKey = new SecureMemoryBuffer(32);
            byte[] rawKey = SecureMemoryUtilities.GenerateSecureRandomBytes(32);
            try
            {
                _protectedKey.Write(rawKey);
                ProtectBuffer();
            }
            finally
            {
                SecureMemoryUtilities.SecureZero(rawKey);
            }

            _creationTime = DateTime.UtcNow;
            _expirationTime = _creationTime.Add(_lifetime);
            _state = KeyState.Activated;
        }
    }

    public void LoadSessionKey(byte[] rawKey)
    {
        if (rawKey == null) throw new ArgumentNullException(nameof(rawKey));
        if (rawKey.Length != 32) throw new ArgumentException("Session key must be 32 bytes.", nameof(rawKey));

        lock (_lock)
        {
            DestroyKey();

            _protectedKey = new SecureMemoryBuffer(32);
            _protectedKey.Write(rawKey);
            ProtectBuffer();

            _creationTime = DateTime.UtcNow;
            _expirationTime = _creationTime.Add(_lifetime);
            _state = KeyState.Activated;
        }
    }

    private void ProtectBuffer()
    {
        if (_protectedKey == null) return;

        _protectedKey.UseBuffer(span => {
            unsafe
            {
                fixed (byte* p = span)
                {
                    MemoryProtector.Protect((IntPtr)p, span.Length);
                }
            }
        });
    }

    public byte[]? GetSessionKeyBytes()
    {
        lock (_lock)
        {
            if (_protectedKey == null || _state == KeyState.Expired || _state == KeyState.Destroyed)
                return null;

            _state = KeyState.InUse;

            byte[] keyBytes = new byte[32];
            _protectedKey.UseBuffer(span => {
                unsafe
                {
                    fixed (byte* p = span)
                    {
                        // Temporarily unprotect to copy
                        MemoryProtector.Unprotect((IntPtr)p, span.Length);
                        try
                        {
                            span.CopyTo(keyBytes);
                        }
                        finally
                        {
                            // Re-protect immediately
                            MemoryProtector.Protect((IntPtr)p, span.Length);
                        }
                    }
                }
            });

            return keyBytes;
        }
    }

    public void DestroyKey()
    {
        lock (_lock)
        {
            if (_protectedKey != null)
            {
                _protectedKey.Dispose();
                _protectedKey = null;
            }
            _state = KeyState.Destroyed;
        }
    }

    public bool IsExpired()
    {
        lock (_lock)
        {
            if (_state == KeyState.Destroyed) return true;
            if (DateTime.UtcNow >= _expirationTime)
            {
                _state = KeyState.Expired;
                return true;
            }
            return false;
        }
    }

    public void ForceExpire()
    {
        lock (_lock)
        {
            _state = KeyState.Expired;
            _expirationTime = DateTime.UtcNow;
        }
    }

    public void Dispose()
    {
        DestroyKey();
    }
}
