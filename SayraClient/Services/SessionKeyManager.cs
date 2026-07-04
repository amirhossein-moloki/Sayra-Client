namespace SayraClient.Services;

public class SessionKeyManager
{
    private byte[]? _sessionKey;
    private readonly object _lock = new();

    public void SetSessionKey(byte[] key)
    {
        lock (_lock)
        {
            _sessionKey = (byte[])key.Clone();
        }
    }

    public byte[]? GetSessionKey()
    {
        lock (_lock)
        {
            return _sessionKey != null ? (byte[])_sessionKey.Clone() : null;
        }
    }

    public void ClearSessionKey()
    {
        lock (_lock)
        {
            if (_sessionKey != null)
            {
                Array.Clear(_sessionKey, 0, _sessionKey.Length);
                _sessionKey = null;
            }
        }
    }

    public bool IsAuthenticated => _sessionKey != null;
}
