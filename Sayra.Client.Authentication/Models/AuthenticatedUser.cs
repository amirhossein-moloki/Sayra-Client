using System;
using System.Collections.Generic;
using Sayra.Client.Authentication.Enums;

namespace Sayra.Client.Authentication.Models
{
    public class AuthenticatedUser
    {
        public string Id { get; }
        public string Username { get; }
        public string DisplayName { get; }
        public UserRole Role { get; }
        public IReadOnlyCollection<UserPermission> Permissions { get; }
        public string? Avatar { get; }
        public DateTime? LastLogin { get; }
        public string PreferredLanguage { get; }
        public string PreferredTheme { get; }
        public string? StationId { get; }

        public AuthenticatedUser(
            string id,
            string username,
            string displayName,
            UserRole role,
            IReadOnlyCollection<UserPermission> permissions,
            string? avatar,
            DateTime? lastLogin,
            string preferredLanguage,
            string preferredTheme,
            string? stationId)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Username = username ?? throw new ArgumentNullException(nameof(username));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Role = role;
            Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
            Avatar = avatar;
            LastLogin = lastLogin;
            PreferredLanguage = preferredLanguage ?? throw new ArgumentNullException(nameof(preferredLanguage));
            PreferredTheme = preferredTheme ?? throw new ArgumentNullException(nameof(preferredTheme));
            StationId = stationId;
        }
    }
}
