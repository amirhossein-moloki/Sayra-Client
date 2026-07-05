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

    public Process StartProcess(string path, string arguments = "", string workingDirectory = "", string verb = "")
    {
        _logger.LogInformation("Starting process: {path} with args: {args} in {wd} (Verb: {verb})", path, arguments, workingDirectory, verb);

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? null : workingDirectory,
            Verb = verb,
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
        try
        {
            _logger.LogInformation("Attempting safe kill for process with PID: {pid}", pid);
            var process = Process.GetProcessById(pid);

            // Try close first
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(3000))
                {
                    _logger.LogWarning("Process {pid} did not exit within 3s, force killing...", pid);
                    process.Kill(true); // true for entire process tree
                }
            }
        }
        catch (ArgumentException)
        {
            _logger.LogWarning("Process with PID {pid} not found for killing.", pid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error killing process {pid}", pid);
            throw;
        }
    }

    public void KillProcessByName(string name)
    {
        _logger.LogInformation("Killing processes with name: {name}", name);
        var processes = Process.GetProcessesByName(name);
        foreach (var process in processes)
        {
            try
            {
                KillProcess(process.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill instance of {name} (PID: {pid})", name, process.Id);
            }
        }
    }

    public IEnumerable<object> ListProcesses()
    {
        return Process.GetProcesses()
            .Select(p => {
                try
                {
                    return new {
                        name = p.ProcessName,
                        pid = p.Id,
                        cpu = GetCpuUsage(p)
                    };
                }
                catch
                {
                    return new { name = p.ProcessName, pid = p.Id, cpu = 0.0 };
                }
            })
            .ToList();
    }

    private double GetCpuUsage(Process p)
    {
        // Placeholder for lightweight CPU tracking
        // Accurate CPU tracking usually requires multiple samples over time.
        // For now, we return 0.0 or could implement a basic heuristic if needed.
        return 0.0;
    }
}
