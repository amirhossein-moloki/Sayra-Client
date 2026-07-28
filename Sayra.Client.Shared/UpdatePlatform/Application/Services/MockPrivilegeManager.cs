using System;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Mock implementation of the Privilege and elevation checker for test-only and cross-platform (Linux CI) environments.
    /// </summary>
    public class MockPrivilegeManager : IPrivilegeManager
    {
        private readonly ILogger<MockPrivilegeManager> _logger;
        private bool? _overrideIsAdmin;

        public MockPrivilegeManager(ILogger<MockPrivilegeManager> logger)
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

            _logger.LogInformation("[Mock/CI] Querying emulated privilege status (Simulating Elevated Administrator).");
            return new PrivilegeStatus(true, true, true);
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
