using System;

namespace Sayra.Client.Shared.Runtime.Domain.Models
{
    public class RuntimePolicyOptions
    {
        /// <summary>
        /// Remaining time threshold for warning level 1 in seconds. Default is 300 (5 minutes).
        /// </summary>
        public int WarningThreshold1Seconds { get; set; } = 300;

        /// <summary>
        /// Remaining time threshold for warning level 2 in seconds. Default is 120 (2 minutes).
        /// </summary>
        public int WarningThreshold2Seconds { get; set; } = 120;

        /// <summary>
        /// Expiration grace period in seconds. Default is 15.
        /// </summary>
        public int ExpirationGracePeriodSeconds { get; set; } = 15;

        /// <summary>
        /// Default game launch timeout in seconds. Default is 30.
        /// </summary>
        public int DefaultLaunchTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Validates that configuration values are within acceptable bounds.
        /// </summary>
        public void Validate()
        {
            if (WarningThreshold1Seconds <= 0)
                throw new ArgumentException("WarningThreshold1Seconds must be positive.");
            if (WarningThreshold2Seconds <= 0)
                throw new ArgumentException("WarningThreshold2Seconds must be positive.");
            if (WarningThreshold1Seconds <= WarningThreshold2Seconds)
                throw new ArgumentException("WarningThreshold1 must be strictly greater than WarningThreshold2.");
            if (ExpirationGracePeriodSeconds < 0)
                throw new ArgumentException("ExpirationGracePeriodSeconds cannot be negative.");
            if (DefaultLaunchTimeoutSeconds <= 0)
                throw new ArgumentException("DefaultLaunchTimeoutSeconds must be positive.");
        }
    }
}
