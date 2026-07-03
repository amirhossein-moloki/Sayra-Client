using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class ProcessMonitor
{
    private readonly ProcessManager _processManager;
    private readonly ILogger<ProcessMonitor> _logger;

    public ProcessMonitor(ProcessManager processManager, ILogger<ProcessMonitor> logger)
    {
        _processManager = processManager;
        _logger = logger;
    }

    public IEnumerable<object> GetRunningProcesses()
    {
        return _processManager.ListProcesses();
    }

    // Optional: Add logic to detect forbidden apps or report status periodically
}
