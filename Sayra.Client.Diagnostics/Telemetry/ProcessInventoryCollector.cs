using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Diagnostics.Telemetry
{
    public class ProcessInventoryCollector
    {
        private readonly ILogger<ProcessInventoryCollector> _logger;

        public ProcessInventoryCollector(ILogger<ProcessInventoryCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public List<RunningProcess> Collect(CancellationToken cancellationToken = default)
        {
            var result = new List<RunningProcess>();
            var processes = Process.GetProcesses();

            foreach (var p in processes)
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    string path = "Access Denied";
                    string hash = "N/A";
                    try { path = p.MainModule?.FileName ?? "Unknown"; } catch { }
                    if (!string.IsNullOrEmpty(path) && path != "Access Denied" && path != "Unknown" && File.Exists(path))
                    {
                        hash = CalculateFileHash(path);
                    }
                    result.Add(new RunningProcess { Pid = p.Id, Name = p.ProcessName, Path = path, User = "Unknown", FileHash = hash });
                }
                catch { }
            }
            return result;
        }

        public string CalculateFileHash(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sha = SHA256.Create())
                {
                    byte[] bytes = sha.ComputeHash(stream);
                    return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                return "Hash Failed (Permission Denied)";
            }
        }
    }
}
