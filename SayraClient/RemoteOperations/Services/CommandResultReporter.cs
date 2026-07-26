using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class CommandResultReporter : ICommandResultReporter
    {
        private readonly ILogger<CommandResultReporter> _logger;
        private readonly IAuditLogger _auditLogger;
        private readonly ConcurrentDictionary<Guid, CommandStatus> _statusCache = new();

        public CommandResultReporter(ILogger<CommandResultReporter> logger, IAuditLogger auditLogger)
        {
            _logger = logger;
            _auditLogger = auditLogger;
        }

        public Task ReportResultAsync(CommandResult result)
        {
            if (result.Success)
            {
                _logger.LogInformation("[CommandResultReporter] Command {CommandId} executed successfully. Duration: {Duration}ms. Payload: {Payload}",
                    result.CommandId, result.ExecutionTime.TotalMilliseconds, result.ResultPayload);
                _auditLogger.LogAudit($"[Remote Operations] Command {result.CommandId} completed successfully.");
            }
            else
            {
                _logger.LogError("[CommandResultReporter] Command {CommandId} failed. ErrorCode: {ErrorCode}, Message: {Message}",
                    result.CommandId, result.ErrorCode, result.ErrorMessage);
                _auditLogger.LogSecurity($"[Remote Operations] Command {result.CommandId} failed with error {result.ErrorCode}: {result.ErrorMessage}");
            }
            return Task.CompletedTask;
        }

        public Task SendStatusUpdateAsync(Guid commandId, CommandStatus status)
        {
            _statusCache[commandId] = status;
            _logger.LogInformation("[CommandResultReporter] Command {CommandId} status updated to: {Status}", commandId, status);
            return Task.CompletedTask;
        }

        public CommandStatus GetCachedStatus(Guid commandId)
        {
            return _statusCache.TryGetValue(commandId, out var status) ? status : CommandStatus.Pending;
        }
    }
}
