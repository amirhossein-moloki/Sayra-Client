using System;
using System.Threading;
using System.Threading.Tasks;

namespace SayraClient.Services;

public interface IPowerManagementService
{
    event EventHandler<PowerActionEventArgs>? ActionExecuting;
    event EventHandler<PowerActionEventArgs>? ActionExecuted;
    event EventHandler<PowerActionFailedEventArgs>? ActionFailed;

    Task RestartAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
    Task LogoffAsync(CancellationToken cancellationToken = default);
    Task LockWorkstationAsync(CancellationToken cancellationToken = default);
}

public class PowerActionEventArgs : EventArgs
{
    public string Action { get; }
    public PowerActionEventArgs(string action) => Action = action;
}

public class PowerActionFailedEventArgs : EventArgs
{
    public string Action { get; }
    public Exception Exception { get; }
    public PowerActionFailedEventArgs(string action, Exception exception)
    {
        Action = action;
        Exception = exception;
    }
}
