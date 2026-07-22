using System;
using System.Threading;
using System.Threading.Tasks;

namespace SayraClient.Services;

/// <summary>
/// Production heartbeat statistics.
/// </summary>
public class HeartbeatStats
{
    public long SentCount { get; set; }
    public long ReceivedCount { get; set; }
    public DateTime? LastSentTime { get; set; }
    public DateTime? LastReceivedTime { get; set; }
    public double ConnectionReliability { get; set; } // Percentage of successful heartbeats
    public bool IsConnected { get; set; }
    public int ConsecutiveMissedCount { get; set; }
}

/// <summary>
/// Manages the periodic production heartbeat loop, dynamic intervals, and connection health.
/// </summary>
public interface IHeartbeatManager
{
    /// <summary>
    /// Event fired when an internal/external heartbeat is sent.
    /// </summary>
    event Action<DateTime>? HeartbeatSent;

    /// <summary>
    /// Event fired when an external heartbeat ACK/reply is received from the server.
    /// </summary>
    event Action<DateTime>? HeartbeatReceived;

    /// <summary>
    /// Event fired when a heartbeat fails or is missed.
    /// </summary>
    event Action<string>? HeartbeatFailed;

    /// <summary>
    /// Starts the heartbeat monitoring and periodic transmission.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the heartbeat loop.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Call this to record that an external heartbeat response was successfully received.
    /// </summary>
    void RecordExternalHeartbeatAck();

    /// <summary>
    /// Dynamically adjust the heartbeat interval based on connection quality or server instructions.
    /// </summary>
    void SetInterval(TimeSpan interval);

    /// <summary>
    /// Retrieves current heartbeat interval.
    /// </summary>
    TimeSpan GetInterval();

    /// <summary>
    /// Obtains a snapshot of current heartbeat transmission and quality statistics.
    /// </summary>
    HeartbeatStats GetStats();
}
