using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Thread-safe implementation of a Token Bucket rate-limiter for bandwidth control.
    /// </summary>
    public class BandwidthLimiter : IBandwidthLimiter
    {
        private readonly object _lock = new object();
        private long _maxBytesPerSecond;
        private double _availableTokens;
        private double _maxTokens;
        private long _lastRefillTicks;

        public long MaxBytesPerSecond
        {
            get
            {
                lock (_lock)
                {
                    return _maxBytesPerSecond;
                }
            }
        }

        public BandwidthLimiter()
        {
            // Default 1MB/s
            SetLimit(1024 * 1024);
        }

        public void SetLimit(long bytesPerSecond)
        {
            lock (_lock)
            {
                _maxBytesPerSecond = bytesPerSecond;
                if (bytesPerSecond > 0)
                {
                    // Allow bursting up to 2x the limit, or at least 1 byte
                    _maxTokens = Math.Max(bytesPerSecond * 2, 1);
                    _availableTokens = 0; // Start with 0 tokens to enforce immediate throttling for tests
                }
                else
                {
                    _maxTokens = 0;
                    _availableTokens = 0;
                }
                _lastRefillTicks = Stopwatch.GetTimestamp();
            }
        }

        public async Task LimitAsync(int bytes, CancellationToken cancellationToken = default)
        {
            if (bytes <= 0) return;

            long limit;
            lock (_lock)
            {
                limit = _maxBytesPerSecond;
            }

            if (limit <= 0)
            {
                // Throttling is disabled
                return;
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double delayMs = 0;

                lock (_lock)
                {
                    RefillTokens();

                    if (_availableTokens >= bytes)
                    {
                        _availableTokens -= bytes;
                        return; // Successfully checked out tokens
                    }

                    // Not enough tokens, calculate wait time
                    double missingTokens = bytes - _availableTokens;
                    delayMs = (missingTokens / _maxBytesPerSecond) * 1000.0;
                }

                if (delayMs > 0)
                {
                    // Clamp delay to avoid excessively long sleeps, with a minimum sleep to prevent spinning
                    int sleepTime = (int)Math.Clamp(delayMs, 10, 1000);
                    await Task.Delay(sleepTime, cancellationToken);
                }
            }
        }

        private void RefillTokens()
        {
            long nowTicks = Stopwatch.GetTimestamp();
            long elapsedTicks = nowTicks - _lastRefillTicks;
            if (elapsedTicks <= 0) return;

            double elapsedSeconds = (double)elapsedTicks / Stopwatch.Frequency;
            _lastRefillTicks = nowTicks;

            double tokensToAdd = elapsedSeconds * _maxBytesPerSecond;
            _availableTokens = Math.Min(_maxTokens, _availableTokens + tokensToAdd);
        }
    }
}
