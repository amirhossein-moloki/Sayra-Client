using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Diagnostics.Services
{
    public class LogBatchingManager
    {
        public byte[] CreateCompressedBatch(List<EventLogEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return Array.Empty<byte>();

            var json = JsonSerializer.Serialize(entries);
            var rawBytes = Encoding.UTF8.GetBytes(json);

            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress, true))
                {
                    gzipStream.Write(rawBytes, 0, rawBytes.Length);
                }
                return outputStream.ToArray();
            }
        }

        public List<EventLogEntry> DecompressBatch(byte[] compressedBytes)
        {
            if (compressedBytes == null || compressedBytes.Length == 0)
                return new List<EventLogEntry>();

            using (var inputStream = new MemoryStream(compressedBytes))
            using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                gzipStream.CopyTo(outputStream);
                var json = Encoding.UTF8.GetString(outputStream.ToArray());
                return JsonSerializer.Deserialize<List<EventLogEntry>>(json) ?? new List<EventLogEntry>();
            }
        }
    }
}
