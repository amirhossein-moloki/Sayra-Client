using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Fleet.Administration.Security
{
    public enum AdminRole
    {
        SuperAdministrator,
        FleetAdministrator,
        SupportEngineer,
        Operator,
        Auditor
    }

    public enum AdminPermission
    {
        ViewMachine,
        ExecuteCommand,
        ManagePolicy,
        AccessDiagnostics,
        RemoteSupport,
        ManageFiles,
        ViewAudit
    }

    public record AdminUser
    {
        public string AdministratorId { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public string PasswordHash { get; init; } = string.Empty;
        public AdminRole Role { get; init; }
        public List<AdminPermission> CustomPermissions { get; init; } = new();
    }

    public record AdminSession
    {
        public string SessionId { get; init; } = string.Empty;
        public string AdministratorId { get; init; } = string.Empty;
        public string Token { get; init; } = string.Empty;
        public DateTime ExpiresAt { get; init; }
    }
}
