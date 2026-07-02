using Microsoft.Extensions.Logging;
using SayraClient.Models;

namespace SayraClient.Commands;

public class CommandRouter
{
    private readonly IEnumerable<ICommandHandler> _handlers;
    private readonly ILogger<CommandRouter> _logger;

    public CommandRouter(IEnumerable<ICommandHandler> handlers, ILogger<CommandRouter> logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    public async Task<ExecutionResult?> RouteAsync(CommandModel command, CancellationToken cancellationToken)
    {
        var handler = _handlers.FirstOrDefault(h => h.CanHandle(command.Action));

        if (handler != null)
        {
            return await handler.HandleAsync(command, cancellationToken);
        }

        _logger.LogWarning("No handler found for action: {action}", command.Action);
        return ExecutionResult.Error(command.Action, "Unknown action");
    }
}
