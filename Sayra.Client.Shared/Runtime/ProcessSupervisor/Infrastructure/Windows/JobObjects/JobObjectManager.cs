using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Infrastructure.Windows.JobObjects
{
    public class JobObjectManager : IJobObjectManager
    {
        private readonly ILogger<JobObjectManager> _logger;
        private readonly ConcurrentDictionary<Guid, SafeJobObjectHandle> _jobs = new();
        private bool _disposed;

        public JobObjectManager(ILogger<JobObjectManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Win32 API Constants and Structs
        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        private const uint JOB_OBJECT_LIMIT_JOB_MEMORY = 0x00000200;
        private const uint JOB_OBJECT_LIMIT_AFFINITY = 0x00000004;

        private const uint PROCESS_SET_QUOTA = 0x0100;
        private const uint PROCESS_TERMINATE = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryLimit;
            public UIntPtr PeakJobMemoryLimit;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeJobObjectHandle CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeJobObjectHandle hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            SafeJobObjectHandle hJob,
            int JobObjectInfoClass,
            IntPtr lpJobObjectInfo,
            uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(SafeJobObjectHandle hJob, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        public void CreateJob(Guid runtimeId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(JobObjectManager));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("CreateJob: Not running on Windows. Skipping native Job Object creation.");
                _jobs.TryAdd(runtimeId, new SafeJobObjectHandle(IntPtr.Zero, false));
                return;
            }

            string jobName = $"SAYRA_Job_{runtimeId}";
            _logger.LogInformation("Creating Windows Job Object: '{JobName}' for RuntimeId: '{RuntimeId}'", jobName, runtimeId);

            var jobHandle = CreateJobObject(IntPtr.Zero, jobName);
            if (jobHandle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                _logger.LogError("Failed to create Job Object. Win32 Error: {Error}", error);
                throw new System.ComponentModel.Win32Exception(error, $"Failed to create Job Object with name '{jobName}'");
            }

            // By default, apply Kill-on-close limit immediately to the new Job
            ConfigureKillOnClose(jobHandle, jobName);

            if (!_jobs.TryAdd(runtimeId, jobHandle))
            {
                jobHandle.Dispose();
                _logger.LogWarning("Job Object for RuntimeId: '{RuntimeId}' already exists.", runtimeId);
            }
        }

        private void ConfigureKillOnClose(SafeJobObjectHandle jobHandle, string jobName)
        {
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            int size = Marshal.SizeOf(info);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, ptr, false);
                if (!SetInformationJobObject(jobHandle, JobObjectExtendedLimitInformation, ptr, (uint)size))
                {
                    int error = Marshal.GetLastWin32Error();
                    _logger.LogError("Failed to configure KillOnClose limit for Job Object: '{JobName}'. Win32 Error: {Error}", jobName, error);
                    throw new System.ComponentModel.Win32Exception(error, $"Failed to set KillOnClose limit on Job Object '{jobName}'");
                }
                _logger.LogInformation("Kill-on-close protection successfully enabled for Job Object: '{JobName}'", jobName);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public void AssignProcess(Guid runtimeId, int processId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(JobObjectManager));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("AssignProcess: Not running on Windows. Skipping native AssignProcess.");
                return;
            }

            if (!_jobs.TryGetValue(runtimeId, out var jobHandle) || jobHandle.IsInvalid)
            {
                _logger.LogError("Cannot assign process {ProcessId}. Job Object not found for RuntimeId '{RuntimeId}'", processId, runtimeId);
                throw new InvalidOperationException($"No active Job Object found for RuntimeId '{runtimeId}'");
            }

            _logger.LogInformation("Assigning Process ID: {ProcessId} to Job Object for RuntimeId: '{RuntimeId}'", processId, runtimeId);

            IntPtr processHandle = OpenProcess(PROCESS_SET_QUOTA | PROCESS_TERMINATE, false, processId);
            if (processHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                _logger.LogError("Failed to OpenProcess with ID: {ProcessId} for assignment. Win32 Error: {Error}", processId, error);
                throw new System.ComponentModel.Win32Exception(error, $"Failed to open process {processId} for Job Object assignment.");
            }

            try
            {
                if (!AssignProcessToJobObject(jobHandle, processHandle))
                {
                    int error = Marshal.GetLastWin32Error();
                    _logger.LogError("Failed to AssignProcessToJobObject for Process ID: {ProcessId}. Win32 Error: {Error}", processId, error);
                    throw new System.ComponentModel.Win32Exception(error, $"Failed to assign process {processId} to Job Object.");
                }
                _logger.LogInformation("Successfully assigned Process ID: {ProcessId} to Job Object.", processId);
            }
            finally
            {
                CloseHandle(processHandle);
            }
        }

        public void ConfigureLimits(Guid runtimeId, long maxMemoryBytes, ulong cpuAffinityMask)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(JobObjectManager));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("ConfigureLimits: Not running on Windows. Skipping native ConfigureLimits.");
                return;
            }

            if (!_jobs.TryGetValue(runtimeId, out var jobHandle) || jobHandle.IsInvalid)
            {
                _logger.LogError("Cannot configure limits. Job Object not found for RuntimeId '{RuntimeId}'", runtimeId);
                throw new InvalidOperationException($"No active Job Object found for RuntimeId '{runtimeId}'");
            }

            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            uint flags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            if (maxMemoryBytes > 0)
            {
                flags |= JOB_OBJECT_LIMIT_JOB_MEMORY;
                info.JobMemoryLimit = new UIntPtr((ulong)maxMemoryBytes);
                _logger.LogInformation("Setting memory limit to {MemoryBytes} bytes on Job Object for RuntimeId '{RuntimeId}'", maxMemoryBytes, runtimeId);
            }

            if (cpuAffinityMask > 0)
            {
                flags |= JOB_OBJECT_LIMIT_AFFINITY;
                info.BasicLimitInformation.Affinity = new UIntPtr(cpuAffinityMask);
                _logger.LogInformation("Setting CPU affinity mask to {AffinityMask} on Job Object for RuntimeId '{RuntimeId}'", cpuAffinityMask, runtimeId);
            }

            info.BasicLimitInformation.LimitFlags = flags;

            int size = Marshal.SizeOf(info);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, ptr, false);
                if (!SetInformationJobObject(jobHandle, JobObjectExtendedLimitInformation, ptr, (uint)size))
                {
                    int error = Marshal.GetLastWin32Error();
                    _logger.LogError("Failed to configure limits on Job Object. Win32 Error: {Error}", error);
                    throw new System.ComponentModel.Win32Exception(error, "Failed to configure limits on Job Object.");
                }
                _logger.LogInformation("Successfully configured limits on Job Object for RuntimeId '{RuntimeId}'", runtimeId);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public void TerminateJob(Guid runtimeId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(JobObjectManager));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("TerminateJob: Not running on Windows. Skipping native TerminateJob.");
                return;
            }

            if (_jobs.TryGetValue(runtimeId, out var jobHandle))
            {
                _logger.LogInformation("Terminating Windows Job Object for RuntimeId: '{RuntimeId}'", runtimeId);
                if (jobHandle != null && !jobHandle.IsInvalid)
                {
                    if (!TerminateJobObject(jobHandle, 0))
                    {
                        int error = Marshal.GetLastWin32Error();
                        // 6 is ERROR_INVALID_HANDLE, which might happen if already terminated
                        if (error != 6)
                        {
                            _logger.LogWarning("Failed to terminate Job Object. Win32 Error: {Error}", error);
                        }
                    }
                    jobHandle.Dispose();
                }
                _jobs.TryRemove(runtimeId, out _);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            foreach (var runtimeId in _jobs.Keys)
            {
                try
                {
                    TerminateJob(runtimeId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error terminating job {RuntimeId} during dispose.", runtimeId);
                }
            }

            _jobs.Clear();
            _disposed = true;
        }
    }
}