using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Sayra.Client.LocalAdmin.Authentication
{
    public class AdminSessionManager : IAdminSessionManager
    {
        private readonly ConcurrentDictionary<string, AdminSession> _sessions = new();
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(15);

        public string CreateSession(string username, TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(username))
                throw new ArgumentNullException(nameof(username));

            CleanExpiredSessions();

            byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
            string token = Convert.ToBase64String(tokenBytes);

            var session = new AdminSession
            {
                Token = token,
                Username = username,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                TimeoutDuration = timeout ?? _defaultTimeout
            };

            _sessions[token] = session;
            return token;
        }

        public bool ValidateSession(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            CleanExpiredSessions();

            if (_sessions.TryGetValue(token, out var session))
            {
                if (DateTime.UtcNow - session.LastActivityAt > session.TimeoutDuration)
                {
                    _sessions.TryRemove(token, out _);
                    return false;
                }

                // Sliding expiration: Extend session on active validation
                session.LastActivityAt = DateTime.UtcNow;
                return true;
            }

            return false;
        }

        public void RevokeSession(string token)
        {
            if (!string.IsNullOrEmpty(token))
            {
                _sessions.TryRemove(token, out _);
            }
        }

        public void CleanExpiredSessions()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _sessions)
            {
                if (now - kvp.Value.LastActivityAt > kvp.Value.TimeoutDuration)
                {
                    _sessions.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    public class AdminSession
    {
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public TimeSpan TimeoutDuration { get; set; }
    }
}
