using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the download status and location details for a single package chunk.
    /// </summary>
    public class DownloadChunk
    {
        public int Index { get; set; }
        public long Offset { get; set; }
        public long SizeBytes { get; set; }
        public string Sha256Checksum { get; set; } = string.Empty;
        public string LocalFilePath { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public long BytesDownloaded { get; set; }
    }
}
