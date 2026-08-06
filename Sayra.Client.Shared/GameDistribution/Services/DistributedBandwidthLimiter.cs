using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;

namespace Sayra.Client.Shared.GameDistribution.Services
{
    public enum TransferPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    public class DistributedBandwidthLimiter
    {
        private readonly IBandwidthLimiter _lanLimiter;
        private readonly IBandwidthLimiter _wanLimiter;

        public DistributedBandwidthLimiter(IBandwidthLimiter lanLimiter, IBandwidthLimiter wanLimiter)
        {
            _lanLimiter = lanLimiter ?? throw new ArgumentNullException(nameof(lanLimiter));
            _wanLimiter = wanLimiter ?? throw new ArgumentNullException(nameof(wanLimiter));
        }

        public void SetLanLimit(long bytesPerSecond) => _lanLimiter.SetLimit(bytesPerSecond);
        public void SetWanLimit(long bytesPerSecond) => _wanLimiter.SetLimit(bytesPerSecond);

        public async Task LimitLanAsync(int bytes, TransferPriority priority = TransferPriority.Normal, CancellationToken cancellationToken = default)
        {
            // Incorporate priority weighting if desired (e.g., critical gets 0 delay, low gets multiplier delay)
            int weightAdjustedBytes = priority switch
            {
                TransferPriority.Critical => (int)(bytes * 0.2),
                TransferPriority.High => (int)(bytes * 0.5),
                TransferPriority.Normal => bytes,
                TransferPriority.Low => (int)(bytes * 1.5),
                _ => bytes
            };

            await _lanLimiter.LimitAsync(weightAdjustedBytes, cancellationToken);
        }

        public async Task LimitWanAsync(int bytes, TransferPriority priority = TransferPriority.Normal, CancellationToken cancellationToken = default)
        {
            int weightAdjustedBytes = priority switch
            {
                TransferPriority.Critical => (int)(bytes * 0.2),
                TransferPriority.High => (int)(bytes * 0.5),
                TransferPriority.Normal => bytes,
                TransferPriority.Low => (int)(bytes * 1.5),
                _ => bytes
            };

            await _wanLimiter.LimitAsync(weightAdjustedBytes, cancellationToken);
        }
    }
}
