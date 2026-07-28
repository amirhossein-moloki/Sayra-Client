using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Governs enterprise-grade download bandwidth limiting and throttling using a Token Bucket rate-limiting algorithm.
    /// </summary>
    public interface IBandwidthLimiter
    {
        /// <summary>
        /// Gets the current maximum download speed in bytes per second.
        /// </summary>
        long MaxBytesPerSecond { get; }

        /// <summary>
        /// Configures the bandwidth limit.
        /// </summary>
        /// <param name="bytesPerSecond">The bandwidth limit in bytes per second. 0 or negative values disable throttling.</param>
        void SetLimit(long bytesPerSecond);

        /// <summary>
        /// Checks out the specified number of bytes, throttling the caller if the limit is exceeded.
        /// </summary>
        /// <param name="bytes">The number of bytes to allocate from the token bucket.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous wait.</returns>
        Task LimitAsync(int bytes, CancellationToken cancellationToken = default);
    }
}
