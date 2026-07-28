using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents a metric for generic update operation telemetry.
    /// </summary>
    public class UpdateOperationMetric
    {
        public string OperationName { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}
