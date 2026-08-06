using System.Threading;

namespace Sayra.Client.Shared.GameDistribution.Telemetry
{
    public static class DistributedCacheTelemetry
    {
        private static long _cacheSizeBytes;
        private static long _hitCount;
        private static long _missCount;
        private static double _transferSpeedBps;
        private static long _wanBytesSaved;
        private static int _peerCount;
        private static long _failedTransfersCount;

        public static long CacheSizeBytes
        {
            get => Interlocked.Read(ref _cacheSizeBytes);
            set => Interlocked.Exchange(ref _cacheSizeBytes, value);
        }

        public static long HitCount
        {
            get => Interlocked.Read(ref _hitCount);
            set => Interlocked.Exchange(ref _hitCount, value);
        }

        public static long MissCount
        {
            get => Interlocked.Read(ref _missCount);
            set => Interlocked.Exchange(ref _missCount, value);
        }

        public static double TransferSpeedBps
        {
            get => Volatile.Read(ref _transferSpeedBps);
            set => Volatile.Write(ref _transferSpeedBps, value);
        }

        public static long WanBytesSaved
        {
            get => Interlocked.Read(ref _wanBytesSaved);
            set => Interlocked.Exchange(ref _wanBytesSaved, value);
        }

        public static int PeerCount
        {
            get => Volatile.Read(ref _peerCount);
            set => Volatile.Write(ref _peerCount, value);
        }

        public static long FailedTransfersCount
        {
            get => Interlocked.Read(ref _failedTransfersCount);
            set => Interlocked.Exchange(ref _failedTransfersCount, value);
        }

        public static double HitRate => (HitCount + MissCount) == 0 ? 0.0 : (double)HitCount / (HitCount + MissCount);

        public static void RecordHit() => Interlocked.Increment(ref _hitCount);
        public static void RecordMiss() => Interlocked.Increment(ref _missCount);
        public static void RecordFailedTransfer() => Interlocked.Increment(ref _failedTransfersCount);
        public static void RecordWanBytesSaved(long bytes) => Interlocked.Add(ref _wanBytesSaved, bytes);
    }
}
