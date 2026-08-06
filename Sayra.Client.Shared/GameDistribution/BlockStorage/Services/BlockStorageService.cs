using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Interfaces;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Models;

namespace Sayra.Client.Shared.GameDistribution.BlockStorage.Services
{
    public class BlockStorageService : IBlockStorageService
    {
        private readonly IBlockRepository _repository;
        private readonly IContentHasher _hasher;
        private readonly ILogger<BlockStorageService> _logger;
        private readonly string _storageDir;

        public BlockStorageService(
            IBlockRepository repository,
            IContentHasher hasher,
            ILogger<BlockStorageService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _storageDir = string.IsNullOrEmpty(appData)
                ? Path.Combine(AppContext.BaseDirectory, "Data", "BlockStorage")
                : Path.Combine(appData, "SAYRA_Client", "BlockStorage");

            try
            {
                if (!Directory.Exists(_storageDir))
                {
                    Directory.CreateDirectory(_storageDir);
                }
                string testFile = Path.Combine(_storageDir, ".write_test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch
            {
                _storageDir = Path.Combine(Path.GetTempPath(), "SAYRA_Client", "BlockStorage");
                if (!Directory.Exists(_storageDir))
                {
                    Directory.CreateDirectory(_storageDir);
                }
            }
        }

        private string GetBlockPath(string blockId) => Path.Combine(_storageDir, $"{blockId}.block");

        public async Task<IEnumerable<ContentBlock>> SplitFileIntoBlocksAsync(
            string filePath,
            string gameId,
            string packageId,
            string version,
            long blockSize = 1024 * 1024,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("Source file for splitting not found.", filePath);

            _logger.LogInformation("Splitting file '{FilePath}' into blocks of size {BlockSize} bytes.", filePath, blockSize);
            var blocks = new List<ContentBlock>();

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                long fileLength = fileStream.Length;
                long offset = 0;
                int index = 0;

                var buffer = new byte[blockSize];

                while (offset < fileLength)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int bytesRead = await fileStream.ReadAsync(buffer, 0, (int)Math.Min(blockSize, fileLength - offset), cancellationToken);
                    if (bytesRead <= 0) break;

                    byte[] actualData = new byte[bytesRead];
                    Array.Copy(buffer, actualData, bytesRead);

                    string hash = _hasher.ComputeHash(actualData);
                    string blockId = $"{packageId}_{index}_{hash}";

                    var block = new ContentBlock
                    {
                        BlockId = blockId,
                        Size = bytesRead,
                        Sha256Hash = hash,
                        Version = version,
                        GameId = gameId,
                        PackageId = packageId,
                        CreationTime = DateTime.UtcNow
                    };

                    await SaveBlockBytesAsync(blockId, actualData, cancellationToken);
                    await _repository.SaveAsync(block, cancellationToken);

                    blocks.Add(block);

                    offset += bytesRead;
                    index++;
                }
            }

            _logger.LogInformation("Successfully split file into {Count} blocks.", blocks.Count);
            return blocks;
        }

        public async Task SaveBlockBytesAsync(string blockId, byte[] data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(blockId)) throw new ArgumentException("Block ID cannot be null or empty.", nameof(blockId));
            if (data == null) throw new ArgumentNullException(nameof(data));

            string path = GetBlockPath(blockId);
            string? dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllBytesAsync(path, data, cancellationToken);
        }

        public async Task<byte[]> GetBlockBytesAsync(string blockId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(blockId)) throw new ArgumentException("Block ID cannot be null or empty.", nameof(blockId));
            string path = GetBlockPath(blockId);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Block file not found for ID '{blockId}'.", path);
            }

            return await File.ReadAllBytesAsync(path, cancellationToken);
        }

        public async Task<bool> VerifyBlockAsync(string blockId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(blockId)) return false;

            var metadata = await _repository.GetAsync(blockId, cancellationToken);
            if (metadata == null)
            {
                _logger.LogWarning("Verification failed: Metadata not found for block ID '{BlockId}'.", blockId);
                return false;
            }

            string path = GetBlockPath(blockId);
            if (!File.Exists(path))
            {
                _logger.LogWarning("Verification failed: Physical file not found for block ID '{BlockId}'.", blockId);
                return false;
            }

            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            string actualHash = _hasher.ComputeHash(bytes);

            bool isMatch = string.Equals(actualHash, metadata.Sha256Hash, StringComparison.OrdinalIgnoreCase);
            if (!isMatch)
            {
                _logger.LogError("Verification failed: Integrity violation for block ID '{BlockId}'. Expected hash '{Expected}', actual '{Actual}'.",
                    blockId, metadata.Sha256Hash, actualHash);
            }

            return isMatch;
        }

        public Task DeleteBlockAsync(string blockId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(blockId)) return Task.CompletedTask;

            string path = GetBlockPath(blockId);
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete physical block file at '{Path}'.", path);
                }
            }

            return _repository.DeleteAsync(blockId, cancellationToken);
        }

        public async Task<IEnumerable<string>> QueryMissingBlocksAsync(
            string gameId,
            IEnumerable<string> requiredBlockIds,
            CancellationToken cancellationToken = default)
        {
            if (requiredBlockIds == null) return Enumerable.Empty<string>();

            var missing = new List<string>();
            foreach (var blockId in requiredBlockIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isVerified = await VerifyBlockAsync(blockId, cancellationToken);
                if (!isVerified)
                {
                    missing.Add(blockId);
                }
            }

            return missing;
        }
    }
}
