using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Fleet.Administration.Security
{
    public interface IAuthorizationService
    {
        bool HasPermission(AdminUser user, AdminPermission permission);
        IReadOnlyList<AdminPermission> GetPermissionsForRole(AdminRole role);
    }

    public class AuthorizationService : IAuthorizationService
    {
        private static readonly Dictionary<AdminRole, List<AdminPermission>> RolePermissions = new()
        {
            {
                AdminRole.SuperAdministrator,
                new List<AdminPermission>
                {
                    AdminPermission.ViewMachine,
                    AdminPermission.ExecuteCommand,
                    AdminPermission.ManagePolicy,
                    AdminPermission.AccessDiagnostics,
                    AdminPermission.RemoteSupport,
                    AdminPermission.ManageFiles,
                    AdminPermission.ViewAudit
                }
            },
            {
                AdminRole.FleetAdministrator,
                new List<AdminPermission>
                {
                    AdminPermission.ViewMachine,
                    AdminPermission.ExecuteCommand,
                    AdminPermission.ManagePolicy,
                    AdminPermission.AccessDiagnostics,
                    AdminPermission.ViewAudit
                }
            },
            {
                AdminRole.SupportEngineer,
                new List<AdminPermission>
                {
                    AdminPermission.ViewMachine,
                    AdminPermission.ExecuteCommand,
                    AdminPermission.AccessDiagnostics,
                    AdminPermission.RemoteSupport,
                    AdminPermission.ManageFiles
                }
            },
            {
                AdminRole.Operator,
                new List<AdminPermission>
                {
                    AdminPermission.ViewMachine,
                    AdminPermission.ExecuteCommand
                }
            },
            {
                AdminRole.Auditor,
                new List<AdminPermission>
                {
                    AdminPermission.ViewMachine,
                    AdminPermission.ViewAudit
                }
            }
        };

        public bool HasPermission(AdminUser user, AdminPermission permission)
        {
            if (user == null) return false;

            // Check standard permissions for role
            if (RolePermissions.TryGetValue(user.Role, out var permissions) && permissions.Contains(permission))
            {
                return true;
            }

            // Check custom custom-assigned permissions
            return user.CustomPermissions.Contains(permission);
        }

        public IReadOnlyList<AdminPermission> GetPermissionsForRole(AdminRole role)
        {
            if (RolePermissions.TryGetValue(role, out var permissions))
            {
                return permissions.AsReadOnly();
            }
            return Array.Empty<AdminPermission>();
        }
    }
}
