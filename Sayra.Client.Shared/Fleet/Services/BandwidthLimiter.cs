using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Interface for bandwidth throttling and management.
    /// </summary>
    public interface IBandwidthLimiter
    {
        /// <summary>
        /// Limits the transmission of the specified bytes based on transfer category/priority.
        /// </summary>
        Task LimitBytesAsync(int count, bool isEmergency = false, bool isBackground = false, CancellationToken ct = default);

        /// <summary>
        /// Dynamically updates the maximum allowed transfer rate in bytes per second.
        /// </summary>
        void SetMaxRate(long bytesPerSecond);
    }

    /// <summary>
    /// High-performance thread-safe token bucket Bandwidth Limiter supporting adaptive throttling and priority.
    /// </summary>
    public class BandwidthLimiter : IBandwidthLimiter
    {
        private readonly object _lock = new();
        private long _maxRateBytesPerSec;
        private double _availableTokens;
        private long _lastTokenRefillTicks;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        /// <summary>
        /// Initializes a new instance of BandwidthLimiter.
        /// </summary>
        /// <param name="maxRateBytesPerSec">Limit ceiling in bytes/sec. Zero represents unlimited.</param>
        public BandwidthLimiter(long maxRateBytesPerSec = 0)
        {
            _maxRateBytesPerSec = maxRateBytesPerSec;
            _availableTokens = maxRateBytesPerSec;
            _lastTokenRefillTicks = _stopwatch.ElapsedTicks;
        }

        /// <summary>
        /// Dynamically updates the maximum allowed transfer rate in bytes per second.
        /// </summary>
        public void SetMaxRate(long bytesPerSecond)
        {
            lock (_lock)
            {
                _maxRateBytesPerSec = bytesPerSecond;
                if (_availableTokens > bytesPerSecond)
                {
                    _availableTokens = bytesPerSecond;
                }
            }
        }

        /// <summary>
        /// Throttles transmission by waiting if the requested bytes exceed the token bucket size.
        /// </summary>
        public async Task LimitBytesAsync(int count, bool isEmergency = false, bool isBackground = false, CancellationToken ct = default)
        {
            if (count <= 0) return;

            // Emergency transfers bypass standard throttling limits completely.
            if (isEmergency) return;

            long limitRate;
            lock (_lock)
            {
                limitRate = _maxRateBytesPerSec;
            }

            if (limitRate <= 0) return;

            // Background transfers are restricted to 50% of the maximum allowed rate.
            if (isBackground)
            {
                limitRate = Math.Max(1024, limitRate / 2);
            }

            while (count > 0)
            {
                ct.ThrowIfCancellationRequested();

                double sleepMs = 0;
                lock (_lock)
                {
                    RefillTokens(limitRate);

                    // Only consume full integer tokens to prevent truncation loop issues where count remains unmodified
                    double consumed = Math.Min(count, Math.Floor(_availableTokens));
                    if (consumed >= 1.0)
                    {
                        _availableTokens -= consumed;
                        count -= (int)consumed;
                    }

                    if (count <= 0)
                    {
                        break;
                    }

                    // Calculate sleep time to refill at least some tokens
                    sleepMs = (1.0 / limitRate) * 1000.0;
                }

                if (sleepMs > 0)
                {
                    // Clamp sleep time to avoid overly long sleeps in a single task
                    int waitMs = (int)Math.Min(1000, Math.Max(1, sleepMs));
                    await Task.Delay(waitMs, ct).ConfigureAwait(false);
                }
            }
        }

        private void RefillTokens(long limitRate)
        {
            long nowTicks = _stopwatch.ElapsedTicks;
            long elapsedTicks = nowTicks - _lastTokenRefillTicks;
            if (elapsedTicks <= 0) return;

            double elapsedSec = (double)elapsedTicks / Stopwatch.Frequency;
            double tokensToAdd = elapsedSec * limitRate;

            _availableTokens = Math.Min(limitRate, _availableTokens + tokensToAdd);
            _lastTokenRefillTicks = nowTicks;
        }
    }
}
