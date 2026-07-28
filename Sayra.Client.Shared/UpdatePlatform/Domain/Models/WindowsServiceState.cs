using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the operational state of a Windows service.
    /// </summary>
    public enum WindowsServiceState
    {
        /// <summary>
        /// Service state is unknown or the service was not found.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The service is not running.
        /// </summary>
        Stopped = 1,

        /// <summary>
        /// The service is starting.
        /// </summary>
        StartPending = 2,

        /// <summary>
        /// The service is stopping.
        /// </summary>
        StopPending = 3,

        /// <summary>
        /// The service is running.
        /// </summary>
        Running = 4,

        /// <summary>
        /// The service continue is pending.
        /// </summary>
        ContinuePending = 5,

        /// <summary>
        /// The service pause is pending.
        /// </summary>
        PausePending = 6,

        /// <summary>
        /// The service is paused.
        /// </summary>
        Paused = 7
    }
}
