namespace Sayra.Client.Shared.Models.Recovery.Policies
{
    /// <summary>
    /// Defines the backoff algorithms supported for scheduling retry attempts in recovery operations.
    /// </summary>
    public enum BackoffStrategy
    {
        /// <summary>
        /// Constant delay between successive recovery retry attempts.
        /// </summary>
        Constant,

        /// <summary>
        /// Delay increases linearly (e.g., 5s, 10s, 15s) with each subsequent attempt.
        /// </summary>
        Linear,

        /// <summary>
        /// Delay increases exponentially (e.g., 2s, 4s, 8s, 16s) with each subsequent attempt.
        /// </summary>
        Exponential,

        /// <summary>
        /// Delay increases exponentially and applies a randomized jitter factor to prevent retry storms.
        /// </summary>
        ExponentialWithJitter
    }
}
