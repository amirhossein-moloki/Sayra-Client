using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.RemoteAssistance
{
    /// <summary>
    /// Production-grade implementation of IRemoteSupportService responsible for registering, managing, and tracking session request contexts.
    /// </summary>
    public class RemoteSupportEngine : IRemoteSupportService
    {
        private readonly RemoteSessionCoordinator _coordinator;
        private readonly ILogger<RemoteSupportEngine> _logger;

        /// <summary>
        /// Initializes a new instance of RemoteSupportEngine.
        /// </summary>
        public RemoteSupportEngine(
            RemoteSessionCoordinator coordinator,
            ILogger<RemoteSupportEngine> logger)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<RemoteSession> RequestSupportSessionAsync(
            string machineId,
            SupportSessionType sessionType,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentNullException(nameof(machineId));

            var requestId = Guid.NewGuid().ToString();
            _logger.LogInformation("Creating Support Session Request: ID={Id}, Target={Machine}, Type={Type}",
                requestId, machineId, sessionType);

            var req = new RemoteSessionRequest
            {
                RequestId = requestId,
                AdministratorId = "Admin-Core-Client",
                MachineId = machineId,
                Reason = "Administrative interactive remote management",
                TimestampUtc = DateTime.UtcNow
            };

            _coordinator.RegisterRequest(req);

            var session = new RemoteSession
            {
                SessionId = requestId,
                TargetMachineId = machineId,
                ConnectionType = sessionType,
                Status = RemoteSessionStatus.Requested,
                AllowedPermissions = SupportPermission.ViewOnly,
                CreatedAtUtc = DateTime.UtcNow
            };

            return Task.FromResult(session);
        }
    }
}
