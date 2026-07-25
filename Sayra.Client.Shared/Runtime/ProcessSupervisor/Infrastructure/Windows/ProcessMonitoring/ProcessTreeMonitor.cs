using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Infrastructure.Windows.ProcessMonitoring
{
    public class ProcessTreeMonitor : IProcessTreeMonitor
    {
        private readonly ILogger<ProcessTreeMonitor> _logger;

        public event Action<Guid, ProcessNode>? UnexpectedProcessDetected;

        public ProcessTreeMonitor(ILogger<ProcessTreeMonitor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Win32 Toolhelp32 P/Invokes
        private const uint TH32CS_SNAPPROCESS = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        public Task<IEnumerable<ProcessNode>> GetDescendantsAsync(int rootProcessId)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("GetDescendantsAsync: Not running on Windows. Returning empty list.");
                return Task.FromResult<IEnumerable<ProcessNode>>(new List<ProcessNode>());
            }

            var descendants = new List<ProcessNode>();
            IntPtr hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (hSnapshot == IntPtr.Zero || hSnapshot == new IntPtr(-1))
            {
                _logger.LogWarning("Failed to create Toolhelp32 snapshot.");
                return Task.FromResult<IEnumerable<ProcessNode>>(descendants);
            }

            try
            {
                var parentToChildren = new Dictionary<uint, List<PROCESSENTRY32>>();
                var pe32 = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };

                if (Process32First(hSnapshot, ref pe32))
                {
                    do
                    {
                        uint parentId = pe32.th32ParentProcessID;
                        if (!parentToChildren.ContainsKey(parentId))
                        {
                            parentToChildren[parentId] = new List<PROCESSENTRY32>();
                        }
                        parentToChildren[parentId].Add(pe32);
                    } while (Process32Next(hSnapshot, ref pe32));
                }

                // Recursively fetch descendants starting from the root process ID
                FetchDescendantsRecursive((uint)rootProcessId, parentToChildren, descendants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while tracing process tree descendants.");
            }
            finally
            {
                CloseHandle(hSnapshot);
            }

            return Task.FromResult<IEnumerable<ProcessNode>>(descendants);
        }

        private void FetchDescendantsRecursive(
            uint parentId,
            Dictionary<uint, List<PROCESSENTRY32>> parentToChildren,
            List<ProcessNode> results)
        {
            if (parentToChildren.TryGetValue(parentId, out var children))
            {
                foreach (var child in children)
                {
                    var node = CreateProcessNode(child);
                    results.Add(node);

                    // Recurse into this child's children
                    FetchDescendantsRecursive(child.th32ProcessID, parentToChildren, results);
                }
            }
        }

        private ProcessNode CreateProcessNode(PROCESSENTRY32 pe)
        {
            int pid = (int)pe.th32ProcessID;
            int ppid = (int)pe.th32ParentProcessID;
            string name = pe.szExeFile;
            string path = string.Empty;
            DateTime startTime = DateTime.UtcNow;
            string status = "Running";

            try
            {
                using (var proc = Process.GetProcessById(pid))
                {
                    startTime = proc.StartTime;
                    path = proc.MainModule?.FileName ?? string.Empty;
                    if (proc.HasExited)
                    {
                        status = "Stopped";
                    }
                }
            }
            catch
            {
                // Process may have exited or is highly privileged, fall back
                status = "Unknown";
            }

            return new ProcessNode
            {
                ProcessId = pid,
                ParentProcessId = ppid,
                ProcessName = name,
                ExecutablePath = string.IsNullOrEmpty(path) ? name : path,
                StartTime = startTime,
                Status = status
            };
        }

        public void TriggerUnexpectedProcess(Guid runtimeId, ProcessNode node)
        {
            _logger.LogWarning("Unexpected/Unauthorized child process detected for Runtime: {RuntimeId}. PID: {Pid}, Name: {Name}", runtimeId, node.ProcessId, node.ProcessName);
            UnexpectedProcessDetected?.Invoke(runtimeId, node);
        }
    }
}
