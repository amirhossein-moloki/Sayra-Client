using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Represents a model for a locking process returned by the Restart Manager.
    /// </summary>
    public class LockingProcess
    {
        public uint ProcessId { get; set; }
        public string AppName { get; set; } = string.Empty;
        public string ServiceShortName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Integrates with the native Windows Restart Manager to handle process locking and graceful app restarting.
    /// </summary>
    public interface IRestartManagerService
    {
        /// <summary>
        /// Detects any active file locks on specified paths.
        /// </summary>
        /// <param name="paths">The physical file paths to scan.</param>
        /// <returns>A list of processes locking the files.</returns>
        List<LockingProcess> DetectFileLocks(IEnumerable<string> paths);

        /// <summary>
        /// Registers resources, shuts down locking applications gracefully, and restarts them.
        /// </summary>
        /// <param name="paths">The physical file paths to register.</param>
        /// <returns>True if shutdown and registration succeeded; otherwise, false.</returns>
        bool ShutdownApplications(IEnumerable<string> paths);

        /// <summary>
        /// Restarts previously shutdown application resources.
        /// </summary>
        /// <returns>True if restart operations succeeded; otherwise, false.</returns>
        bool RestartApplications();
    }
}
