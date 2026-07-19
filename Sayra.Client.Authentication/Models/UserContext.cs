using System;
using System.Collections.Generic;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.Authentication.Enums;

namespace Sayra.Client.Authentication.Models
{
    public class UserContext : IUserContext
    {
        private readonly object _lock = new object();

        private string? _userId;
        private string? _username;
        private string? _displayName;
        private UserRole? _role;
        private IReadOnlyCollection<UserPermission>? _permissions;
        private string? _authenticationType;
        private DateTime? _loginTime;
        private DateTime _lastActivity = DateTime.UtcNow;
        private string? _sessionId;

        public string? UserId
        {
            get { lock (_lock) return _userId; }
        }

        public string? Username
        {
            get { lock (_lock) return _username; }
        }

        public string? DisplayName
        {
            get { lock (_lock) return _displayName; }
        }

        public UserRole? Role
        {
            get { lock (_lock) return _role; }
        }

        public IReadOnlyCollection<UserPermission>? Permissions
        {
            get { lock (_lock) return _permissions; }
        }

        public string? AuthenticationType
        {
            get { lock (_lock) return _authenticationType; }
        }

        public DateTime? LoginTime
        {
            get { lock (_lock) return _loginTime; }
        }

        public DateTime LastActivity
        {
            get { lock (_lock) return _lastActivity; }
        }

        public string? SessionId
        {
            get { lock (_lock) return _sessionId; }
        }

        public bool IsAuthenticated
        {
            get { lock (_lock) return _userId != null; }
        }

        public void SetUser(AuthenticatedUser user, string authenticationType, string? sessionId = null)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            lock (_lock)
            {
                _userId = user.Id;
                _username = user.Username;
                _displayName = user.DisplayName;
                _role = user.Role;
                _permissions = user.Permissions;
                _authenticationType = authenticationType;
                _loginTime = DateTime.UtcNow;
                _lastActivity = DateTime.UtcNow;
                _sessionId = sessionId ?? Guid.NewGuid().ToString();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _userId = null;
                _username = null;
                _displayName = null;
                _role = null;
                _permissions = null;
                _authenticationType = null;
                _loginTime = null;
                _lastActivity = DateTime.UtcNow;
                _sessionId = null;
            }
        }

        public void RecordActivity()
        {
            lock (_lock)
            {
                _lastActivity = DateTime.UtcNow;
            }
        }
    }
}
