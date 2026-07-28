using System;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Performs privilege, administrative rights, and UAC elevation checks.
    /// Strictly handles production Windows-only security operations.
    /// </summary>
    public class PrivilegeManager : IPrivilegeManager
    {
        private readonly ILogger<PrivilegeManager> _logger;

        public PrivilegeManager(ILogger<PrivilegeManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public PrivilegeStatus GetCurrentPrivilegeStatus()
        {
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
