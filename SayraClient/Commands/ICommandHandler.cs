using SayraClient.Models;

namespace SayraClient.Commands;

public interface ICommandHandler
{
    Task<ExecutionResult?> HandleAsync(CommandModel command, CancellationToken cancellationToken);
    bool CanHandle(string action);
}
