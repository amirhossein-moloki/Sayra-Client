using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services.Windows;

public interface IWindowsEventLogService
{
    void WriteEntry(string message, EventLogEntryType type, int eventId = 1000);
}

public class WindowsEventLogService : IWindowsEventLogService
{
    private readonly ILogger<WindowsEventLogService> _logger;
    private const string SourceName = "SAYRA_Client";
    private const string LogName = "Application";

    public WindowsEventLogService(ILogger<WindowsEventLogService> logger)
    {
        _logger = logger;
        InitializeEventSource();
    }

    private void InitializeEventSource()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogInformation("Not running on Windows. Skipping Windows Event Log source initialization.");
            return;
        }

        try
        {
            if (!EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(SourceName, LogName);
                _logger.LogInformation("Successfully registered custom event source '{Source}' in Log '{Log}'.", SourceName, LogName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register custom event source '{Source}' in Log '{Log}'. This is normal if not running with Administrator privileges.", SourceName, LogName);
        }
    }

    public void WriteEntry(string message, EventLogEntryType type, int eventId = 1000)
    {
        _logger.LogInformation("[WindowsEventLog] [{Type}] (ID: {EventId}): {Message}", type, eventId, message);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            EventLog.WriteEntry(SourceName, message, type, eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write entry to Windows Event Log.");
        }
    }
}
