using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.Authentication.Models;

namespace Sayra.Client.Authentication.Providers
{
    public class CachedAuthenticationProvider : IAuthenticationProvider
    {
        private static readonly ConcurrentDictionary<string, (string Password, AuthenticatedUser User)> _cache =
            new ConcurrentDictionary<string, (string, AuthenticatedUser)>(StringComparer.OrdinalIgnoreCase);

        public string ProviderName => "Cached";

        public bool CanHandle(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            return _cache.ContainsKey(username);
        }

        public Task<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            if (!CanHandle(username, password))
            {
                return Task.FromResult(AuthenticationResult.CreateFailure("کاربری با این مشخصات در حافظه موقت آفلاین یافت نشد."));
            }

            if (_cache.TryGetValue(username, out var cachedData))
            {
                if (cachedData.Password == password)
                {
                    // Update last login
                    var updatedUser = new AuthenticatedUser(
                        cachedData.User.Id,
                        cachedData.User.Username,
                        cachedData.User.DisplayName,
                        cachedData.User.Role,
                        cachedData.User.Permissions,
                        cachedData.User.Avatar,
                        DateTime.UtcNow, // update
                        cachedData.User.PreferredLanguage,
                        cachedData.User.PreferredTheme,
                        cachedData.User.StationId
                    );

                    return Task.FromResult(AuthenticationResult.CreateSuccess(updatedUser, ProviderName, requiresSynchronization: true));
                }
            }

            return Task.FromResult(AuthenticationResult.CreateFailure("گذرواژه آفلاین نادرست است."));
        }

        public static void CacheUser(string username, string password, AuthenticatedUser user)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || user == null) return;
            _cache[username] = (password, user);
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}
