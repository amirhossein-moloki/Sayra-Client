using System;
using System.Diagnostics;
using System.Threading;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Thread-safe class that aggregates downloaded bytes, estimates download speed, computes ETA, and raises change events.
    /// </summary>
    public class ProgressReporter : IProgressReporter
    {
        private readonly object _lock = new object();
        private Guid _jobId;
        private long _totalSizeBytes;
        private long _bytesDownloaded;
        private long _lastBytesDownloaded;
        private long _lastTimeTicks;
        private double _smoothedSpeed; // Exponential moving average of bytes per second
        private DownloadProgress _currentProgress = new DownloadProgress();

        public event EventHandler<DownloadProgress>? ProgressChanged;

        public DownloadProgress CurrentProgress
        {
            get
            {
                lock (_lock)
                {
                    return new DownloadProgress
                    {
                        JobId = _currentProgress.JobId,
                        BytesDownloaded = _currentProgress.BytesDownloaded,
                        TotalSizeBytes = _currentProgress.TotalSizeBytes,
                        DownloadSpeedBytesPerSecond = _currentProgress.DownloadSpeedBytesPerSecond,
                        EstimatedTimeRemaining = _currentProgress.EstimatedTimeRemaining
                    };
                }
            }
        }

        public void Reset(Guid jobId, long totalSizeBytes)
        {
            lock (_lock)
            {
                _jobId = jobId;
                _totalSizeBytes = totalSizeBytes;
                _bytesDownloaded = 0;
                _lastBytesDownloaded = 0;
                _lastTimeTicks = Stopwatch.GetTimestamp();
                _smoothedSpeed = 0;
                _currentProgress = new DownloadProgress
                {
                    JobId = _jobId,
                    BytesDownloaded = 0,
                    TotalSizeBytes = _totalSizeBytes,
                    DownloadSpeedBytesPerSecond = 0,
                    EstimatedTimeRemaining = TimeSpan.Zero
                };
            }
        }

        public void ReportProgress(long bytesDownloaded)
        {
            DownloadProgress snap;

            lock (_lock)
            {
                _bytesDownloaded = bytesDownloaded;

                long nowTicks = Stopwatch.GetTimestamp();
                long elapsedTicks = nowTicks - _lastTimeTicks;
                double elapsedSeconds = (double)elapsedTicks / Stopwatch.Frequency;

                // Update speed estimates at reasonable intervals (e.g., every 250ms) to avoid chaotic spikes,
                // or if complete.
                if (elapsedSeconds >= 0.25 || _bytesDownloaded >= _totalSizeBytes)
                {
                    long deltaBytes = _bytesDownloaded - _lastBytesDownloaded;
                    double currentSpeed = elapsedSeconds > 0 ? deltaBytes / elapsedSeconds : 0;

                    if (_smoothedSpeed == 0)
                    {
                        _smoothedSpeed = currentSpeed;
                    }
                    else
                    {
                        // EMA weight
                        _smoothedSpeed = (_smoothedSpeed * 0.7) + (currentSpeed * 0.3);
                    }

                    _lastBytesDownloaded = _bytesDownloaded;
                    _lastTimeTicks = nowTicks;
                }

                TimeSpan eta = TimeSpan.Zero;
                if (_smoothedSpeed > 0 && _bytesDownloaded < _totalSizeBytes)
                {
                    long remainingBytes = _totalSizeBytes - _bytesDownloaded;
                    double etaSeconds = remainingBytes / _smoothedSpeed;
                    if (etaSeconds > 0 && etaSeconds < int.MaxValue)
                    {
                        eta = TimeSpan.FromSeconds(etaSeconds);
                    }
                }

                _currentProgress = new DownloadProgress
                {
                    JobId = _jobId,
                    BytesDownloaded = _bytesDownloaded,
                    TotalSizeBytes = _totalSizeBytes,
                    DownloadSpeedBytesPerSecond = _smoothedSpeed,
                    EstimatedTimeRemaining = eta
                };

                snap = _currentProgress;
            }

            ProgressChanged?.Invoke(this, snap);
        }
    }
}
