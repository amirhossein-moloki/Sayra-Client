using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.ServiceProcess;
using System.Threading;

namespace Sayra.Client.Updater;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Sayra Client Updater starting...");

        string? packagePath = GetArg(args, "--package");
        string? backupPath = GetArg(args, "--backup");
        string? serviceName = GetArg(args, "--service") ?? "Sayra Client";

        if (string.IsNullOrEmpty(packagePath) || string.IsNullOrEmpty(backupPath))
        {
            Console.WriteLine("Usage: SayraUpdater.exe --package <path> --backup <path> [--service <name>]");
            return 1;
        }

        try
        {
            // 1. Stop Service
            StopService(serviceName);

            // 2. Wait for process to exit
            WaitForProcessToExit("SayraClient");

            // 3. Apply Update
            ApplyUpdate(packagePath);

            // 4. Start Service
            StartService(serviceName);

            Console.WriteLine("Update applied successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update failed: {ex.Message}");
            Rollback(backupPath);
            StartService(serviceName);
            return 1;
        }
    }

    static string? GetArg(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        if (index >= 0 && index < args.Length - 1)
            return args[index + 1];
        return null;
    }

    static void StopService(string serviceName)
    {
        if (!OperatingSystem.IsWindows()) return;

        Console.WriteLine($"Stopping service {serviceName}...");
        using var sc = new ServiceController(serviceName);
        if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
        {
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        }
        Console.WriteLine("Service stopped.");
    }

    static void StartService(string serviceName)
    {
        if (!OperatingSystem.IsWindows()) return;

        Console.WriteLine($"Starting service {serviceName}...");
        using var sc = new ServiceController(serviceName);
        if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
        {
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        }
        Console.WriteLine("Service started.");
    }

    static void WaitForProcessToExit(string processName)
    {
        Console.WriteLine($"Waiting for {processName} processes to exit...");
        int attempts = 0;
        while (Process.GetProcessesByName(processName).Any() && attempts < 10)
        {
            Thread.Sleep(1000);
            attempts++;
        }

        // Force kill if still running
        foreach (var p in Process.GetProcessesByName(processName))
        {
            try { p.Kill(); } catch { }
        }
    }

    static void ApplyUpdate(string packagePath)
    {
        string targetDir = AppContext.BaseDirectory;
        Console.WriteLine($"Extracting {packagePath} to {targetDir}...");

        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            // Skip the updater itself if it's in the package, to avoid file in use
            if (entry.FullName.Contains("SayraUpdater.exe", StringComparison.OrdinalIgnoreCase))
                continue;

            string destinationPath = Path.Combine(targetDir, entry.FullName);
            string? directory = Path.GetDirectoryName(destinationPath);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (!string.IsNullOrEmpty(entry.Name))
            {
                entry.ExtractToFile(destinationPath, true);
            }
        }
    }

    static void Rollback(string backupPath)
    {
        Console.WriteLine($"Rolling back from {backupPath}...");
        string targetDir = AppContext.BaseDirectory;

        foreach (var file in Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(backupPath, file);
            string destinationPath = Path.Combine(targetDir, relativePath);

            string? directory = Path.GetDirectoryName(destinationPath);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.Copy(file, destinationPath, true);
        }
    }
}
