using System;
using Sayra.Client.Authentication.Models;
using Sayra.Client.Authentication.Enums;

namespace Sayra.Client.Authentication.Events
{
    public class AuthenticationStartedEventArgs : EventArgs
    {
        public string Username { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public AuthenticationStartedEventArgs(string username)
        {
            Username = username;
        }
    }

    public class AuthenticationSucceededEventArgs : EventArgs
    {
        public AuthenticatedUser User { get; }
        public string AuthenticationType { get; }
        public string SessionId { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public AuthenticationSucceededEventArgs(AuthenticatedUser user, string authenticationType, string sessionId)
        {
            User = user ?? throw new ArgumentNullException(nameof(user));
            AuthenticationType = authenticationType;
            SessionId = sessionId;
        }
    }

    public class AuthenticationFailedEventArgs : EventArgs
    {
        public string Username { get; }
        public string Reason { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public AuthenticationFailedEventArgs(string username, string reason)
        {
            Username = username;
            Reason = reason;
        }
    }

    public class AuthenticationExpiredEventArgs : EventArgs
    {
        public AuthenticatedUser User { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public AuthenticationExpiredEventArgs(AuthenticatedUser user)
        {
            User = user ?? throw new ArgumentNullException(nameof(user));
        }
    }

    public class LogoutStartedEventArgs : EventArgs
    {
        public AuthenticatedUser? User { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public LogoutStartedEventArgs(AuthenticatedUser? user)
        {
            User = user;
        }
    }

    public class LogoutCompletedEventArgs : EventArgs
    {
        public string? Username { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public LogoutCompletedEventArgs(string? username)
        {
            Username = username;
        }
    }

    public class SessionExpiredEventArgs : EventArgs
    {
        public string SessionId { get; }
        public string? Username { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public SessionExpiredEventArgs(string sessionId, string? username)
        {
            SessionId = sessionId;
            Username = username;
        }
    }

    public class PermissionChangedEventArgs : EventArgs
    {
        public string Username { get; }
        public System.Collections.Generic.IReadOnlyCollection<UserPermission> NewPermissions { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public PermissionChangedEventArgs(string username, System.Collections.Generic.IReadOnlyCollection<UserPermission> newPermissions)
        {
            Username = username;
            NewPermissions = newPermissions;
        }
    }

    public class RoleChangedEventArgs : EventArgs
    {
        public string Username { get; }
        public UserRole NewRole { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public RoleChangedEventArgs(string username, UserRole newRole)
        {
            Username = username;
            NewRole = newRole;
        }
    }
}
