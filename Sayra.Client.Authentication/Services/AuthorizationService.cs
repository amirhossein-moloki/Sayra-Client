using System;
using System.Linq;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.Authentication.Enums;

namespace Sayra.Client.Authentication.Services
{
    public class AuthorizationService : IAuthorizationService
    {
        private readonly IUserContext _userContext;

        public AuthorizationService(IUserContext userContext)
        {
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        }

        public bool HasPermission(UserPermission permission)
        {
            if (!_userContext.IsAuthenticated || _userContext.Permissions == null)
            {
                return false;
            }

            // High level administrators bypass typical authorization limitations (e.g. SuperAdministrator or Administrator has all permissions)
            if (_userContext.Role == UserRole.SuperAdministrator || _userContext.Role == UserRole.Administrator)
            {
                return true;
            }

            return _userContext.Permissions.Contains(permission);
        }

        public bool HasRole(UserRole role)
        {
            if (!_userContext.IsAuthenticated || _userContext.Role == null)
            {
                return false;
            }

            // Role hierarchy: SuperAdministrator has all roles, Administrator has LocalAdministrator and Guest/Player, etc.
            var currentRole = _userContext.Role.Value;

            if (currentRole == role) return true;

            if (currentRole == UserRole.SuperAdministrator) return true;

            if (currentRole == UserRole.Administrator)
            {
                return role == UserRole.LocalAdministrator || role == UserRole.Player || role == UserRole.Guest;
            }

            if (currentRole == UserRole.LocalAdministrator)
            {
                return role == UserRole.Guest;
            }

            if (currentRole == UserRole.Player)
            {
                return role == UserRole.Guest;
            }

            return false;
        }

        public bool CanAccess(string resource)
        {
            if (string.IsNullOrWhiteSpace(resource)) return false;

            // Simple rule-based resource check
            if (resource.Equals("AdminPanel", StringComparison.OrdinalIgnoreCase))
            {
                return HasPermission(UserPermission.OpenAdminPanel);
            }
            if (resource.Equals("Settings", StringComparison.OrdinalIgnoreCase))
            {
                return HasPermission(UserPermission.ManageSettings);
            }
            if (resource.Equals("GameLibrary", StringComparison.OrdinalIgnoreCase))
            {
                return HasPermission(UserPermission.ManageLibrary);
            }

            return _userContext.IsAuthenticated;
        }

        public bool CanExecute(string action)
        {
            if (string.IsNullOrWhiteSpace(action)) return false;

            // Simple action check mapping
            if (action.Equals("LaunchGame", StringComparison.OrdinalIgnoreCase))
            {
                return HasPermission(UserPermission.LaunchGame);
            }
            if (action.Equals("Backup", StringComparison.OrdinalIgnoreCase))
            {
                return HasPermission(UserPermission.BackupDatabase);
            }
            if (action.Equals("Restore", StringComparison.OrdinalIgnoreCase))
            {
                return HasPermission(UserPermission.RestoreDatabase);
            }

            return _userContext.IsAuthenticated;
        }
    }
}
