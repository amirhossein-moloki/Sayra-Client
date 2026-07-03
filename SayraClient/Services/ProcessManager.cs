using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class ProcessManager
{
    private readonly ILogger<ProcessManager> _logger;

    public ProcessManager(ILogger<ProcessManager> logger)
    {
        _logger = logger;
    }

    public Process StartProcess(string path, string arguments = "")
    {
        _logger.LogInformation("Starting process: {path} with args: {args}", path, arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments,
            UseShellExecute = true // Allows launching games/apps via shell
        };

        var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new Exception($"Failed to start process: {path}");
        }

        return process;
    }

    public void KillProcess(int pid)
    {
        _logger.LogInformation("Killing process with PID: {pid}", pid);
        var process = Process.GetProcessById(pid);
        process.Kill(true);
    }

    public void KillProcessByName(string name)
    {
        _logger.LogInformation("Killing processes with name: {name}", name);
        var processes = Process.GetProcessesByName(name);
        foreach (var process in processes)
        {
            process.Kill(true);
        }
    }

    public IEnumerable<object> ListProcesses()
    {
        return Process.GetProcesses()
            .Select(p => new { name = p.ProcessName, pid = p.Id })
            .ToList();
    }
}
