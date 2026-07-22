using System.Threading.Tasks;

namespace SayraClient.Services;

/// <summary>
/// Coordinates the safe, ordered, and graceful shutdown of the client application.
/// </summary>
public interface IShutdownCoordinator
{
    /// <summary>
    /// Initiates a synchronized graceful shutdown sequence.
    /// </summary>
    /// <param name="reason">The reason for initiating shutdown.</param>
    /// <param name="exitCode">The process exit code.</param>
    Task InitiateShutdownAsync(string reason, int exitCode = 0);

    /// <summary>
    /// Forcefully terminates the process immediately (emergency shutdown fallback).
    /// </summary>
    void EmergencyShutdown(string reason, int exitCode = -1);
}
