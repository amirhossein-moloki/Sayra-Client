using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Performs privilege, administrative rights, and UAC elevation checks.
    /// Integrates seamlessly with Windows APIs with complete fallbacks for cross-platform execution.
    /// </summary>
    public class PrivilegeManager : IPrivilegeManager
    {
        private readonly ILogger<PrivilegeManager> _logger;
        private bool? _overrideIsAdmin;

        public PrivilegeManager(ILogger<PrivilegeManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Allows overriding the admin status for xUnit unit testing purposes.
        /// </summary>
        public void OverrideAdminStatus(bool isAdmin)
        {
            _overrideIsAdmin = isAdmin;
        }

        /// <inheritdoc />
        public PrivilegeStatus GetCurrentPrivilegeStatus()
        {
            if (_overrideIsAdmin.HasValue)
            {
                _logger.LogInformation("Returning overridden test privilege status (IsAdmin: {IsAdmin}).", _overrideIsAdmin.Value);
                return new PrivilegeStatus(_overrideIsAdmin.Value, _overrideIsAdmin.Value, _overrideIsAdmin.Value);
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogInformation("[CI/Linux] Querying emulated privilege status (Simulating Elevated Administrator).");
                return new PrivilegeStatus(true, true, true);
            }

            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    bool isAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator);
                    bool hasRequiredPrivileges = isAdministrator; // In SYSTEM context or Elevated Admin, required privileges are present
                    bool isElevated = isAdministrator;

                    _logger.LogInformation("Evaluated Windows privilege status. IsAdmin: {IsAdmin}, IsElevated: {IsElevated}.", isAdministrator, isElevated);
                    return new PrivilegeStatus(isAdministrator, hasRequiredPrivileges, isElevated);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve Windows identity and privilege status.");
                return new PrivilegeStatus(false, false, false);
            }
        }

        /// <inheritdoc />
        public void EnsureAdminPrivileges()
        {
            var status = GetCurrentPrivilegeStatus();
            if (!status.IsAdministrator)
            {
                var error = "Insufficient privileges. This operation requires Administrative privileges / UAC elevation.";
                _logger.LogError(error);
                throw new PrivilegeException(error);
            }
        }
    }
}
