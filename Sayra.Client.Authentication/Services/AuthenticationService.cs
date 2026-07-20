using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.Authentication.Enums;
using Sayra.Client.Authentication.Events;
using Sayra.Client.Authentication.Exceptions;
using Sayra.Client.Authentication.Models;
using Sayra.Client.Authentication.Providers;

namespace Sayra.Client.Authentication.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IEnumerable<IAuthenticationProvider> _providers;
        private readonly UserContext _userContext;
        private readonly ILogger<AuthenticationService>? _logger;
        private AuthenticationState _state = AuthenticationState.Unauthenticated;

        public event EventHandler<AuthenticationStartedEventArgs>? AuthenticationStarted;
        public event EventHandler<AuthenticationSucceededEventArgs>? AuthenticationSucceeded;
        public event EventHandler<AuthenticationFailedEventArgs>? AuthenticationFailed;
        public event EventHandler<AuthenticationExpiredEventArgs>? AuthenticationExpired;
        public event EventHandler<LogoutStartedEventArgs>? LogoutStarted;
        public event EventHandler<LogoutCompletedEventArgs>? LogoutCompleted;
        public event EventHandler<SessionExpiredEventArgs>? SessionExpired;
        public event EventHandler<PermissionChangedEventArgs>? PermissionChanged;
        public event EventHandler<RoleChangedEventArgs>? RoleChanged;

        public AuthenticationService(
            IEnumerable<IAuthenticationProvider> providers,
            IUserContext userContext,
            ILogger<AuthenticationService>? logger = null)
        {
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _userContext = (userContext as UserContext) ?? throw new ArgumentException("UserContext must be of type UserContext.", nameof(userContext));
            _logger = logger;
        }

        public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                var failure = AuthenticationResult.CreateFailure("نام کاربری نمی‌تواند خالی باشد.");
                RaiseAuthenticationFailed(username, failure.FailureReason!);
                return failure;
            }

            _logger?.LogInformation("Starting authentication process for user: {Username}", username);
            _state = AuthenticationState.Authenticating;
            RaiseAuthenticationStarted(username);

            // Find an appropriate provider
            var provider = _providers.FirstOrDefault(p => p.CanHandle(username, password));
            if (provider == null)
            {
                _logger?.LogWarning("No registered provider could handle credentials for user: {Username}", username);
                _state = AuthenticationState.Unauthenticated;
                var failure = AuthenticationResult.CreateFailure("سیستم احراز هویت مناسب برای این حساب کاربری یافت نشد.");
                RaiseAuthenticationFailed(username, failure.FailureReason!);
                return failure;
            }

            _logger?.LogInformation("Selected authentication provider: {ProviderName} for user: {Username}", provider.ProviderName, username);

            try
            {
                var result = await provider.AuthenticateAsync(username, password);

                if (result.Success && result.AuthenticatedUser != null)
                {
                    _logger?.LogInformation("Successfully authenticated user: {Username} via provider: {ProviderName}", username, provider.ProviderName);

                    // Update local context
                    string sessionId = Guid.NewGuid().ToString();
                    _userContext.SetUser(result.AuthenticatedUser, result.AuthenticationType ?? provider.ProviderName, sessionId);

                    // Cache credentials for subsequent offline logins
                    CachedAuthenticationProvider.CacheUser(username, password, result.AuthenticatedUser);

                    // Determine state
                    if (provider.ProviderName == "Offline" || provider.ProviderName == "Cached")
                    {
                        _state = AuthenticationState.Offline;
                    }
                    else
                    {
                        _state = AuthenticationState.Authenticated;
                    }

                    // Raise success event
                    RaiseAuthenticationSucceeded(result.AuthenticatedUser, result.AuthenticationType ?? provider.ProviderName, sessionId);
                    return result;
                }
                else
                {
                    _logger?.LogWarning("Authentication failed for user: {Username} via provider: {ProviderName}. Reason: {Reason}",
                        username, provider.ProviderName, result.FailureReason);

                    _state = AuthenticationState.Unauthenticated;
                    RaiseAuthenticationFailed(username, result.FailureReason ?? "اطلاعات ورود نادرست است.");
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception occurred during authentication process for user: {Username}", username);
                _state = AuthenticationState.Unauthenticated;
                RaiseAuthenticationFailed(username, ex.Message);
                throw new AuthenticationFailedException($"خطا در فرآیند احراز هویت: {ex.Message}", ex);
            }
        }

        public async Task LogoutAsync()
        {
            var currentUser = _userContext.IsAuthenticated ? GetCurrentUser() : null;
            string? username = currentUser?.Username;

            _logger?.LogInformation("Initiating logout for user: {Username}", username ?? "Unknown");
            RaiseLogoutStarted(currentUser);

            await Task.Delay(100); // Simulate logout cleanup tasks

            _userContext.Clear();
            _state = AuthenticationState.Unauthenticated;

            _logger?.LogInformation("Logout completed for user: {Username}", username ?? "Unknown");
            RaiseLogoutCompleted(username);
        }

        public Task<AuthenticationResult> RefreshAuthenticationAsync()
        {
            if (!_userContext.IsAuthenticated)
            {
                return Task.FromResult(AuthenticationResult.CreateFailure("کاربر فعال جهت بازنشانی نشست یافت نشد."));
            }

            var user = GetCurrentUser()!;
            _logger?.LogInformation("Refreshing session for authenticated user: {Username}", user.Username);
            _userContext.RecordActivity();

            var result = AuthenticationResult.CreateSuccess(user, _userContext.AuthenticationType ?? "Refresh");
            return Task.FromResult(result);
        }

        public Task<bool> ValidateCurrentSessionAsync()
        {
            if (!_userContext.IsAuthenticated)
            {
                return Task.FromResult(false);
            }

            // If session is older than some threshold, it might expire, but let's assume valid.
            _userContext.RecordActivity();
            return Task.FromResult(true);
        }

        public AuthenticatedUser? GetCurrentUser()
        {
            if (!_userContext.IsAuthenticated) return null;

            return new AuthenticatedUser(
                _userContext.UserId!,
                _userContext.Username!,
                _userContext.DisplayName!,
                _userContext.Role!.Value,
                _userContext.Permissions!,
                null, // Avatar
                _userContext.LoginTime,
                "fa",
                "Dark",
                null
            );
        }

        public AuthenticationState GetAuthenticationState()
        {
            return _state;
        }

        // Helper trigger methods
        private void RaiseAuthenticationStarted(string username)
        {
            AuthenticationStarted?.Invoke(this, new AuthenticationStartedEventArgs(username));
        }

        private void RaiseAuthenticationSucceeded(AuthenticatedUser user, string authType, string sessionId)
        {
            AuthenticationSucceeded?.Invoke(this, new AuthenticationSucceededEventArgs(user, authType, sessionId));
        }

        private void RaiseAuthenticationFailed(string username, string reason)
        {
            AuthenticationFailed?.Invoke(this, new AuthenticationFailedEventArgs(username, reason));
        }

        private void RaiseLogoutStarted(AuthenticatedUser? user)
        {
            LogoutStarted?.Invoke(this, new LogoutStartedEventArgs(user));
        }

        private void RaiseLogoutCompleted(string? username)
        {
            LogoutCompleted?.Invoke(this, new LogoutCompletedEventArgs(username));
        }
    }
}
