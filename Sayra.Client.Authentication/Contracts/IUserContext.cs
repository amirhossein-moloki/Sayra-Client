using System;
using System.Collections.Generic;
using Sayra.Client.Authentication.Enums;

namespace Sayra.Client.Authentication.Contracts
{
    public interface IUserContext
    {
        string? UserId { get; }
        string? Username { get; }
        string? DisplayName { get; }
        UserRole? Role { get; }
        IReadOnlyCollection<UserPermission>? Permissions { get; }
        string? AuthenticationType { get; }
        DateTime? LoginTime { get; }
        DateTime LastActivity { get; }
        string? SessionId { get; }

        bool IsAuthenticated { get; }
    }
}
