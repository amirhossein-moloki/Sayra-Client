namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Indicates the result status of a traced operation or span.
    /// </summary>
    public enum TraceResult
    {
        /// <summary>The operation was completed successfully.</summary>
        Success,
        /// <summary>The operation failed due to an exception or error.</summary>
        Failed,
        /// <summary>The operation timed out before completion.</summary>
        Timeout,
        /// <summary>The operation was manually aborted or cancelled.</summary>
        Aborted
    }
}
