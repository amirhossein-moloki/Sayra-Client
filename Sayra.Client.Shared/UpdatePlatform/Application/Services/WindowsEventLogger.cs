using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Writes update-specific operational and security events to the custom Windows Event Log channel (SAYRA_Client_Updates).
    /// Includes graceful fallbacks for non-Windows platforms (Linux CI) and low-privilege execution environments.
    /// </summary>
    public class WindowsEventLogger : IWindowsEventLogger
    {
        private readonly ILogger<WindowsEventLogger> _logger;
        private const string SourceName = "SAYRA_Client_Updates";
        private const string LogName = "SAYRA_Client_Updates";
        private bool _sourceExists = false;

        public WindowsEventLogger(ILogger<WindowsEventLogger> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            InitializeEventSource();
        }

        private void InitializeEventSource()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogInformation("Not running on Windows. Emulating Event Log via ILogger.");
                return;
            }

            try
            {
                // Cache SourceExists check on startup (Performance Hardening)
                _sourceExists = EventLog.SourceExists(SourceName);
                if (!_sourceExists)
                {
                    EventLog.CreateEventSource(SourceName, LogName);
                    _sourceExists = true;
                    _logger.LogInformation("Successfully registered custom event source '{Source}' in Log '{Log}'.", SourceName, LogName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register custom event source '{Source}' in Log '{Log}'. Falling back to standard logging.", SourceName, LogName);
            }
        }

        /// <inheritdoc />
        public void LogInstallationStarted(string targetVersion)
        {
            string message = $"SAYRA Client update installation started for target version: {targetVersion}";
            WriteEntry(message, EventLogEntryType.Information, eventId: 2001);
        }

        /// <inheritdoc />
        public void LogInstallationCompleted(string targetVersion)
        {
            string message = $"SAYRA Client update installation completed successfully for version: {targetVersion}";
            WriteEntry(message, EventLogEntryType.Information, eventId: 2002);
        }

        /// <inheritdoc />
        public void LogRollbackStarted(string failedVersion, string restoredVersion)
        {
            string message = $"SAYRA Client update failed. Starting automated rollback from version {failedVersion} to last stable version: {restoredVersion}";
            WriteEntry(message, EventLogEntryType.Warning, eventId: 2003);
        }

        /// <inheritdoc />
        public void LogRollbackCompleted(string restoredVersion)
        {
            string message = $"SAYRA Client rollback completed successfully. System restored to version: {restoredVersion}";
            WriteEntry(message, EventLogEntryType.Information, eventId: 2004);
        }

        /// <inheritdoc />
        public void LogVerificationFailure(string filePath, string reason)
        {
            string message = $"SAYRA Client file verification failed for path: '{filePath}'. Reason: {reason}";
            WriteEntry(message, EventLogEntryType.Error, eventId: 4001);
        }

        /// <inheritdoc />
        public void LogSecurityFailure(string reason)
        {
            string message = $"SAYRA Client enterprise security validation failed. Potential attack or corruption detected: {reason}";
            WriteEntry(message, EventLogEntryType.Error, eventId: 4002);
        }

        private void WriteEntry(string message, EventLogEntryType type, int eventId)
        {
            // Always output to logger for debugging and CI observability
            switch (type)
            {
                case EventLogEntryType.Error:
                    _logger.LogError("[EventLog][ID: {EventId}] {Message}", eventId, message);
                    break;
                case EventLogEntryType.Warning:
                    _logger.LogWarning("[EventLog][ID: {EventId}] {Message}", eventId, message);
                    break;
                default:
                    _logger.LogInformation("[EventLog][ID: {EventId}] {Message}", eventId, message);
                    break;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            try
            {
                // Avoid calling the expensive EventLog.SourceExists on every single logging entry call
                if (_sourceExists)
                {
                    EventLog.WriteEntry(SourceName, message, type, eventId);
                }
                else
                {
                    EventLog.WriteEntry("Application", $"[{SourceName}] " + message, type, eventId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write entry to Windows Event Log.");
            }
        }
    }
}
