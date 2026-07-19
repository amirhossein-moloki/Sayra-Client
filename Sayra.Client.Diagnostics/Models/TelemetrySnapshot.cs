using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record TelemetrySnapshot(
        DateTime Timestamp,
        HardwareMetrics Metrics
    );
}
