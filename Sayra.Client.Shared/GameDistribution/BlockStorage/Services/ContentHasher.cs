using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Interfaces;

namespace Sayra.Client.Shared.GameDistribution.BlockStorage.Services
{
    public class ContentHasher : IContentHasher
    {
        public string ComputeHash(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(data);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public async Task<string> ComputeHashAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using var sha256 = SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
