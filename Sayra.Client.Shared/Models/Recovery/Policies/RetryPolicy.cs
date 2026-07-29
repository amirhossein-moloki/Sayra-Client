using System;

namespace Sayra.Client.Shared.Models.Recovery.Policies
{
    /// <summary>
    /// Configuration model for managing retries of self-healing actions.
    /// This model is immutable and serializable.
    /// </summary>
    public class RetryPolicy
    {
        /// <summary>
        /// Gets the maximum number of retry attempts before aborting.
        /// </summary>
        public int MaxRetries { get; init; } = 3;

        /// <summary>
        /// Gets the initial delay time before the first retry attempt.
        /// </summary>
        public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Gets the upper ceiling limit for retry delays.
        /// </summary>
        public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets the algorithm strategy used to calculate backoff delay increments.
        /// </summary>
        public BackoffStrategy BackoffStrategy { get; init; } = BackoffStrategy.ExponentialWithJitter;
    }
}
