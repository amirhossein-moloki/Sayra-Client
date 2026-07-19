using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.Authentication.Enums;
using Sayra.Client.Authentication.Models;
using Sayra.Client.LocalAdmin.Services;

namespace Sayra.Client.Authentication.Providers
{
    public class LocalAdminAuthenticationProvider : IAuthenticationProvider
    {
        private readonly ILocalAdminService? _localAdminService;

        public string ProviderName => "LocalAdmin";

        public LocalAdminAuthenticationProvider(ILocalAdminService? localAdminService = null)
        {
            _localAdminService = localAdminService;
        }

        public bool CanHandle(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            string lower = username.ToLowerInvariant();
            return lower == "admin" || lower == "afmin";
        }

        public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            if (!CanHandle(username, password))
            {
                return AuthenticationResult.CreateFailure("This provider cannot handle the specified credentials.");
            }

            try
            {
                bool success = false;
                string? failureReason = null;

                if (_localAdminService != null)
                {
                    var result = await _localAdminService.Authenticate(username, password);
                    success = result.Success;
                    failureReason = result.ErrorReason;
                }
                else
                {
                    // Fallback to static credentials if service is not registered/active (designer/simple test mode)
                    success = (password == "admin");
                    failureReason = success ? null : "Incorrect password.";
                }

                if (success)
                {
                    var role = username.ToLowerInvariant() == "admin" ? UserRole.LocalAdministrator : UserRole.Administrator;
                    var permissions = new List<UserPermission>
                    {
                        UserPermission.OpenAdminPanel,
                        UserPermission.ManageLibrary,
                        UserPermission.ManageUsers,
                        UserPermission.ManageSettings,
                        UserPermission.ShutdownSystem,
                        UserPermission.RestartSystem,
                        UserPermission.BackupDatabase,
                        UserPermission.RestoreDatabase
                    };

                    var user = new AuthenticatedUser(
                        id: $"LA_{username.ToUpperInvariant()}",
                        username: username,
                        displayName: username == "admin" ? "مدیر محلی" : "مدیر اصلی",
                        role: role,
                        permissions: permissions.AsReadOnly(),
                        avatar: "admin_avatar.png",
                        lastLogin: DateTime.UtcNow,
                        preferredLanguage: "fa",
                        preferredTheme: "Dark",
                        stationId: "LocalPC"
                    );

                    return AuthenticationResult.CreateSuccess(user, ProviderName);
                }
                else
                {
                    return AuthenticationResult.CreateFailure(failureReason ?? "اعتبارسنجی مدیر محلی با شکست مواجه شد.");
                }
            }
            catch (Exception ex)
            {
                return AuthenticationResult.CreateFailure($"خطای سیستم در تأیید هویت مدیر: {ex.Message}");
            }
        }
    }
}
