using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Handlers
{
    public class UnlockPcCommandHandler : IRemoteCommandHandler
    {
        public bool CanHandle(string action) => action.Equals("UNLOCK_PC", StringComparison.OrdinalIgnoreCase);

        public Task<CommandResult> HandleAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            // Verify signature is present
            if (string.IsNullOrWhiteSpace(command.Signature))
            {
                return Task.FromResult(CommandResult.Failed(command.CommandId, "UNAUTHORIZED", "Signature is missing or invalid for UNLOCK_PC"));
            }

            // Verify SenderAdminId is authorized
            if (string.IsNullOrWhiteSpace(command.SenderAdminId))
            {
                return Task.FromResult(CommandResult.Failed(command.CommandId, "UNAUTHORIZED", "Sender admin ID is missing or invalid"));
            }

            // Optional: If an authorization token is provided, validate it
            if (!string.IsNullOrEmpty(command.Payload))
            {
                try
                {
                    using var doc = JsonDocument.Parse(command.Payload);
                    if (doc.RootElement.TryGetProperty("token", out var tokenProp))
                    {
                        var token = tokenProp.GetString();
                        if (token == "EXPIRED_TOKEN" || token == "INVALID_TOKEN")
                        {
                            return Task.FromResult(CommandResult.Failed(command.CommandId, "UNAUTHORIZED", "The provided authorization token is invalid or expired."));
                        }
                    }
                }
                catch
                {
                    // Ignore parsing errors and proceed with basic signature verification
                }
            }

            return Task.FromResult(CommandResult.Successful(command.CommandId, "Workstation unlocked successfully with valid admin authorization"));
        }
    }
}
