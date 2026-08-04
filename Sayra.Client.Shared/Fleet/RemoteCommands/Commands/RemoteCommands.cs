using System;
using System.Collections.Generic;
using System.Linq;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.RemoteCommands.Commands
{
    /// <summary>
    /// Registry of all supported Remote Command Action Verbs.
    /// </summary>
    public static class RemoteCommandActions
    {
        /// <summary>Restart the workstation.</summary>
        public const string RestartMachine = "RESTART_MACHINE";
        /// <summary>Shutdown the workstation.</summary>
        public const string ShutdownMachine = "SHUTDOWN_MACHINE";
        /// <summary>Restart a target Windows Service.</summary>
        public const string RestartWindowsService = "RESTART_WINDOWS_SERVICE";
        /// <summary>Restart the core SAYRA Service.</summary>
        public const string RestartSayraService = "RESTART_SAYRA_SERVICE";
        /// <summary>Restart a specific background worker.</summary>
        public const string RestartWorker = "RESTART_WORKER";
        /// <summary>Restart IPC endpoints and listeners.</summary>
        public const string RestartIpc = "RESTART_IPC";
        /// <summary>Restart visual Overlay interfaces.</summary>
        public const string RestartOverlay = "RESTART_OVERLAY";
        /// <summary>Lock down the workstation interface.</summary>
        public const string LockWorkstation = "LOCK_WORKSTATION";
        /// <summary>Unlock the workstation interface.</summary>
        public const string UnlockWorkstation = "UNLOCK_WORKSTATION";
        /// <summary>Force reload configurations from disk or server.</summary>
        public const string ReloadConfiguration = "RELOAD_CONFIGURATION";
        /// <summary>Refresh dynamic configuration parameters.</summary>
        public const string RefreshConfiguration = "REFRESH_CONFIGURATION";
        /// <summary>Synchronize configurations with central master nodes.</summary>
        public const string SyncConfiguration = "SYNC_CONFIGURATION";
        /// <summary>Flush all transient memory caches.</summary>
        public const string FlushCache = "FLUSH_CACHE";
        /// <summary>Trigger an immediate telemetry collection and refresh.</summary>
        public const string RefreshTelemetry = "REFRESH_TELEMETRY";
        /// <summary>Execute an interactive subsystem health assessment.</summary>
        public const string RunHealthCheck = "RUN_HEALTH_CHECK";
        /// <summary>Enter localized maintenance quarantine status.</summary>
        public const string StartMaintenance = "START_MAINTENANCE";
        /// <summary>Exit localized maintenance quarantine status.</summary>
        public const string StopMaintenance = "STOP_MAINTENANCE";
        /// <summary>Purge temporary runtime files and system logs.</summary>
        public const string ClearTemporaryData = "CLEAR_TEMPORARY_DATA";
        /// <summary>Purge downloaded package chunks and caches.</summary>
        public const string ClearDownloads = "CLEAR_DOWNLOADS";
        /// <summary>Execute a custom administrative process command.</summary>
        public const string CustomAdminCommand = "CUSTOM_ADMIN_COMMAND";
    }

    /// <summary>
    /// Represents the immutable core metadata and details of an executing remote command.
    /// </summary>
    public interface IBaseRemoteCommand
    {
        /// <summary>Gets the command tracking identifier.</summary>
        string CommandId { get; }

        /// <summary>Gets the action descriptor verb.</summary>
        string Action { get; }

        /// <summary>Gets the targeted client machine identifier.</summary>
        string TargetMachineId { get; }

        /// <summary>Gets the scheduling priority level.</summary>
        CommandPriority Priority { get; }

        /// <summary>Gets the operator identifier of the executing administrator.</summary>
        string CreatorOperatorId { get; }

        /// <summary>Gets the expiration timestamp for validation windows.</summary>
        DateTime ExpiresAtUtc { get; }

        /// <summary>Gets the correlation identifier for tracking.</summary>
        string CorrelationId { get; }

        /// <summary>Gets the created timestamp.</summary>
        DateTime CreatedAtUtc { get; }

        /// <summary>Gets the underlying parameter collection.</summary>
        List<CommandParameter> Parameters { get; }
    }

    /// <summary>
    /// Abstract base class for all strongly-typed remote commands.
    /// </summary>
    public abstract class RemoteCommandBase : IBaseRemoteCommand
    {
        /// <inheritdoc />
        public string CommandId { get; set; } = Guid.NewGuid().ToString();

        /// <inheritdoc />
        public abstract string Action { get; }

        /// <inheritdoc />
        public string TargetMachineId { get; set; } = string.Empty;

        /// <inheritdoc />
        public CommandPriority Priority { get; set; } = CommandPriority.Normal;

        /// <inheritdoc />
        public string CreatorOperatorId { get; set; } = string.Empty;

        /// <inheritdoc />
        public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(5);

        /// <inheritdoc />
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        /// <inheritdoc />
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <inheritdoc />
        public List<CommandParameter> Parameters { get; set; } = new();

        /// <summary>
        /// Standard parsing utility to read a string parameter.
        /// </summary>
        protected static string GetParamString(IEnumerable<CommandParameter> parameters, string name, string defaultValue = "")
        {
            return parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Value ?? defaultValue;
        }

        /// <summary>
        /// Standard parsing utility to read a boolean parameter.
        /// </summary>
        protected static bool GetParamBool(IEnumerable<CommandParameter> parameters, string name, bool defaultValue = false)
        {
            var val = GetParamString(parameters, name);
            return bool.TryParse(val, out var res) ? res : defaultValue;
        }

        /// <summary>
        /// Standard parsing utility to read an integer parameter.
        /// </summary>
        protected static int GetParamInt(IEnumerable<CommandParameter> parameters, string name, int defaultValue = 0)
        {
            var val = GetParamString(parameters, name);
            return int.TryParse(val, out var res) ? res : defaultValue;
        }
    }

    /// <summary>Command to restart the workstation.</summary>
    public class RestartMachineCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.RestartMachine;
        /// <summary>Gets whether to force shutdown programs immediately.</summary>
        public bool Force { get; init; }
        /// <summary>Gets countdown timeout before restart.</summary>
        public int TimeoutSeconds { get; init; }

        /// <summary>Initializes a new instance of the command.</summary>
        public RestartMachineCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public RestartMachineCommand(IEnumerable<CommandParameter> parameters)
        {
            Force = GetParamBool(parameters, "Force");
            TimeoutSeconds = GetParamInt(parameters, "TimeoutSeconds", 30);
        }
    }

    /// <summary>Command to shutdown the workstation.</summary>
    public class ShutdownMachineCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.ShutdownMachine;
        /// <summary>Gets whether to force shutdown programs immediately.</summary>
        public bool Force { get; init; }
        /// <summary>Gets countdown timeout before shutdown.</summary>
        public int TimeoutSeconds { get; init; }

        /// <summary>Initializes a new instance of the command.</summary>
        public ShutdownMachineCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public ShutdownMachineCommand(IEnumerable<CommandParameter> parameters)
        {
            Force = GetParamBool(parameters, "Force");
            TimeoutSeconds = GetParamInt(parameters, "TimeoutSeconds", 30);
        }
    }

    /// <summary>Command to restart a target Windows service.</summary>
    public class RestartWindowsServiceCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.RestartWindowsService;
        /// <summary>Gets the name of the target service.</summary>
        public string ServiceName { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public RestartWindowsServiceCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public RestartWindowsServiceCommand(IEnumerable<CommandParameter> parameters)
        {
            ServiceName = GetParamString(parameters, "ServiceName");
        }
    }

    /// <summary>Command to restart the SAYRA service.</summary>
    public class RestartSayraServiceCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.RestartSayraService;
        /// <summary>Gets mode of restart (e.g. Graceful, Forced).</summary>
        public string RestartMode { get; init; } = "Graceful";

        /// <summary>Initializes a new instance of the command.</summary>
        public RestartSayraServiceCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public RestartSayraServiceCommand(IEnumerable<CommandParameter> parameters)
        {
            RestartMode = GetParamString(parameters, "RestartMode", "Graceful");
        }
    }

    /// <summary>Command to restart a specific background worker.</summary>
    public class RestartWorkerCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.RestartWorker;
        /// <summary>Gets the name of the worker process or thread.</summary>
        public string WorkerName { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public RestartWorkerCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public RestartWorkerCommand(IEnumerable<CommandParameter> parameters)
        {
            WorkerName = GetParamString(parameters, "WorkerName");
        }
    }

    /// <summary>Command to restart IPC endpoints.</summary>
    public class RestartIpcCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.RestartIpc;
        /// <summary>Gets name of IPC Pipe to reset.</summary>
        public string PipeName { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public RestartIpcCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public RestartIpcCommand(IEnumerable<CommandParameter> parameters)
        {
            PipeName = GetParamString(parameters, "PipeName");
        }
    }

    /// <summary>Command to restart UI overlay rendering.</summary>
    public class RestartOverlayCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.RestartOverlay;
        /// <summary>Gets targeted monitor index.</summary>
        public int MonitorIndex { get; init; }

        /// <summary>Initializes a new instance of the command.</summary>
        public RestartOverlayCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public RestartOverlayCommand(IEnumerable<CommandParameter> parameters)
        {
            MonitorIndex = GetParamInt(parameters, "MonitorIndex");
        }
    }

    /// <summary>Command to lock the workstation interface.</summary>
    public class LockWorkstationCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.LockWorkstation;
        /// <summary>Gets reason for lock action.</summary>
        public string Reason { get; init; } = string.Empty;
        /// <summary>Gets visual message shown on screen lock overlay.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public LockWorkstationCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public LockWorkstationCommand(IEnumerable<CommandParameter> parameters)
        {
            Reason = GetParamString(parameters, "Reason");
            Message = GetParamString(parameters, "Message");
        }
    }

    /// <summary>Command to unlock the workstation interface.</summary>
    public class UnlockWorkstationCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.UnlockWorkstation;
        /// <summary>Gets cryptographic or pin-based unlock code.</summary>
        public string UnlockCode { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public UnlockWorkstationCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public UnlockWorkstationCommand(IEnumerable<CommandParameter> parameters)
        {
            UnlockCode = GetParamString(parameters, "UnlockCode");
        }
    }

    /// <summary>Command to reload configuration settings.</summary>
    public class ReloadConfigurationCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.ReloadConfiguration;
        /// <summary>Gets specific version requested.</summary>
        public string ConfigVersion { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public ReloadConfigurationCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public ReloadConfigurationCommand(IEnumerable<CommandParameter> parameters)
        {
            ConfigVersion = GetParamString(parameters, "ConfigVersion");
        }
    }

    /// <summary>Command to refresh configuration properties.</summary>
    public class RefreshConfigurationCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.RefreshConfiguration;
        /// <summary>Gets specific configuration section.</summary>
        public string SectionName { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public RefreshConfigurationCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public RefreshConfigurationCommand(IEnumerable<CommandParameter> parameters)
        {
            SectionName = GetParamString(parameters, "SectionName");
        }
    }

    /// <summary>Command to synchronize configuration with servers.</summary>
    public class SyncConfigurationCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.SyncConfiguration;
        /// <summary>Gets URL endpoint for synchronization.</summary>
        public string SyncTargetUrl { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public SyncConfigurationCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public SyncConfigurationCommand(IEnumerable<CommandParameter> parameters)
        {
            SyncTargetUrl = GetParamString(parameters, "SyncTargetUrl");
        }
    }

    /// <summary>Command to flush transient memory caches.</summary>
    public class FlushCacheCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.FlushCache;
        /// <summary>Gets key to flush. If empty, flushes all.</summary>
        public string CacheKey { get; init; } = string.Empty;
        /// <summary>Gets whether to flush entire cache levels.</summary>
        public bool All { get; init; }

        /// <summary>Initializes a new instance of the command.</summary>
        public FlushCacheCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public FlushCacheCommand(IEnumerable<CommandParameter> parameters)
        {
            CacheKey = GetParamString(parameters, "CacheKey");
            All = GetParamBool(parameters, "All");
        }
    }

    /// <summary>Command to trigger an immediate telemetry refresh.</summary>
    public class RefreshTelemetryCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.RefreshTelemetry;
        /// <summary>Gets specific collector filter name.</summary>
        public string CollectorName { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public RefreshTelemetryCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public RefreshTelemetryCommand(IEnumerable<CommandParameter> parameters)
        {
            CollectorName = GetParamString(parameters, "CollectorName");
        }
    }

    /// <summary>Command to execute subsystem health verification scans.</summary>
    public class RunHealthCheckCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.RunHealthCheck;
        /// <summary>Gets targeted subsystem identifier.</summary>
        public string SubsystemId { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public RunHealthCheckCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public RunHealthCheckCommand(IEnumerable<CommandParameter> parameters)
        {
            SubsystemId = GetParamString(parameters, "SubsystemId");
        }
    }

    /// <summary>Command to start maintenance quarantine.</summary>
    public class StartMaintenanceCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.StartMaintenance;
        /// <summary>Gets specific maintenance task profile identifier.</summary>
        public string MaintenanceId { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public StartMaintenanceCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public StartMaintenanceCommand(IEnumerable<CommandParameter> parameters)
        {
            MaintenanceId = GetParamString(parameters, "MaintenanceId");
        }
    }

    /// <summary>Command to stop maintenance quarantine.</summary>
    public class StopMaintenanceCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.StopMaintenance;
        /// <summary>Gets specific maintenance task profile identifier.</summary>
        public string MaintenanceId { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public StopMaintenanceCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public StopMaintenanceCommand(IEnumerable<CommandParameter> parameters)
        {
            MaintenanceId = GetParamString(parameters, "MaintenanceId");
        }
    }

    /// <summary>Command to clear localized temporary logs and files.</summary>
    public class ClearTemporaryDataCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.ClearTemporaryData;
        /// <summary>Gets targeted path filters.</summary>
        public string Paths { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public ClearTemporaryDataCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public ClearTemporaryDataCommand(IEnumerable<CommandParameter> parameters)
        {
            Paths = GetParamString(parameters, "Paths");
        }
    }

    /// <summary>Command to clear downloaded package cache files.</summary>
    public class ClearDownloadsCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.ClearDownloads;
        /// <summary>Gets threshold age days for clearing.</summary>
        public int AgeDays { get; init; }

        /// <summary>Initializes a new instance of the command.</summary>
        public ClearDownloadsCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public ClearDownloadsCommand(IEnumerable<CommandParameter> parameters)
        {
            AgeDays = GetParamInt(parameters, "AgeDays");
        }
    }

    /// <summary>Command to execute standard native customized commands.</summary>
    public class CustomAdminCommand : RemoteCommandBase
    {
        /// <inheritdoc />
        public override string Action => RemoteCommandActions.CustomAdminCommand;
        /// <summary>Gets direct terminal process executable command text.</summary>
        public string CommandText { get; init; } = string.Empty;
        /// <summary>Gets target arguments or options.</summary>
        public string Arguments { get; init; } = string.Empty;

        /// <summary>Initializes a new instance of the command.</summary>
        public CustomAdminCommand() { }

        /// <summary>Initializes a new instance from parameters.</summary>
        public CustomAdminCommand(IEnumerable<CommandParameter> parameters)
        {
            CommandText = GetParamString(parameters, "CommandText");
            Arguments = GetParamString(parameters, "Arguments");
        }
    }

    /// <summary>
    /// Factory for mapping from domain model to strongly-typed remote commands.
    /// </summary>
    public static class RemoteCommandFactory
    {
        /// <summary>
        /// Instantiates the corresponding strongly-typed RemoteCommand representation.
        /// </summary>
        public static IBaseRemoteCommand Map(RemoteCommand domainCmd)
        {
            RemoteCommandBase target = domainCmd.Action.ToUpperInvariant() switch
            {
                RemoteCommandActions.RestartMachine => new RestartMachineCommand(domainCmd.Parameters),
                RemoteCommandActions.ShutdownMachine => new ShutdownMachineCommand(domainCmd.Parameters),
                RemoteCommandActions.RestartWindowsService => new RestartWindowsServiceCommand(domainCmd.Parameters),
                RemoteCommandActions.RestartSayraService => new RestartSayraServiceCommand(domainCmd.Parameters),
                RemoteCommandActions.RestartWorker => new RestartWorkerCommand(domainCmd.Parameters),
                RemoteCommandActions.RestartIpc => new RestartIpcCommand(domainCmd.Parameters),
                RemoteCommandActions.RestartOverlay => new RestartOverlayCommand(domainCmd.Parameters),
                RemoteCommandActions.LockWorkstation => new LockWorkstationCommand(domainCmd.Parameters),
                RemoteCommandActions.UnlockWorkstation => new UnlockWorkstationCommand(domainCmd.Parameters),
                RemoteCommandActions.ReloadConfiguration => new ReloadConfigurationCommand(domainCmd.Parameters),
                RemoteCommandActions.RefreshConfiguration => new RefreshConfigurationCommand(domainCmd.Parameters),
                RemoteCommandActions.SyncConfiguration => new SyncConfigurationCommand(domainCmd.Parameters),
                RemoteCommandActions.FlushCache => new FlushCacheCommand(domainCmd.Parameters),
                RemoteCommandActions.RefreshTelemetry => new RefreshTelemetryCommand(domainCmd.Parameters),
                RemoteCommandActions.RunHealthCheck => new RunHealthCheckCommand(domainCmd.Parameters),
                RemoteCommandActions.StartMaintenance => new StartMaintenanceCommand(domainCmd.Parameters),
                RemoteCommandActions.StopMaintenance => new StopMaintenanceCommand(domainCmd.Parameters),
                RemoteCommandActions.ClearTemporaryData => new ClearTemporaryDataCommand(domainCmd.Parameters),
                RemoteCommandActions.ClearDownloads => new ClearDownloadsCommand(domainCmd.Parameters),
                RemoteCommandActions.CustomAdminCommand => new CustomAdminCommand(domainCmd.Parameters),
                _ => throw new ArgumentException($"Unsupported remote command action verb: {domainCmd.Action}", nameof(domainCmd))
            };

            target.CommandId = domainCmd.CommandId;
            target.TargetMachineId = domainCmd.TargetMachineId;
            target.Priority = domainCmd.Priority;
            target.CreatorOperatorId = domainCmd.CreatorOperatorId;
            target.ExpiresAtUtc = domainCmd.ExpiresAtUtc;
            target.Parameters = domainCmd.Parameters;
            target.CorrelationId = Guid.NewGuid().ToString(); // runtime tracking context
            target.CreatedAtUtc = DateTime.UtcNow;

            return target;
        }
    }
}
