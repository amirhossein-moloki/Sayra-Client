using System;
using System.Threading.Tasks;
using Sayra.Client.Authentication.Enums;
using Sayra.Client.Authentication.Events;
using Sayra.Client.Authentication.Models;

namespace Sayra.Client.Authentication.Contracts
{
    public interface IAuthenticationService
    {
        event EventHandler<AuthenticationStartedEventArgs>? AuthenticationStarted;
        event EventHandler<AuthenticationSucceededEventArgs>? AuthenticationSucceeded;
        event EventHandler<AuthenticationFailedEventArgs>? AuthenticationFailed;
        event EventHandler<AuthenticationExpiredEventArgs>? AuthenticationExpired;
        event EventHandler<LogoutStartedEventArgs>? LogoutStarted;
        event EventHandler<LogoutCompletedEventArgs>? LogoutCompleted;
        event EventHandler<SessionExpiredEventArgs>? SessionExpired;
        event EventHandler<PermissionChangedEventArgs>? PermissionChanged;
        event EventHandler<RoleChangedEventArgs>? RoleChanged;

        Task<AuthenticationResult> AuthenticateAsync(string username, string password);
        Task LogoutAsync();
        Task<AuthenticationResult> RefreshAuthenticationAsync();
        Task<bool> ValidateCurrentSessionAsync();
        AuthenticatedUser? GetCurrentUser();
        AuthenticationState GetAuthenticationState();
    }
}
