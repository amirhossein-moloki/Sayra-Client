using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Interfaces;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Models;
using Sayra.Client.Shared.GameDistribution.Cache.Interfaces;
using Sayra.Client.Shared.GameDistribution.Repair.Interfaces;
using Sayra.Client.Shared.GameDistribution.Transfer.Interfaces;

namespace Sayra.Client.Shared.GameDistribution.Repair.Services
{
    public class GameRepairService : IGameRepairService
    {
        private readonly IBlockStorageService _storageService;
        private readonly IDistributedCacheManager _cacheManager;
        private readonly IPeerTransferService _transferService;
        private readonly ILogger<GameRepairService> _logger;

        public GameRepairService(
            IBlockStorageService storageService,
            IDistributedCacheManager cacheManager,
            IPeerTransferService transferService,
            ILogger<GameRepairService> logger)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _transferService = transferService ?? throw new ArgumentNullException(nameof(transferService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> RepairGameAsync(
            string gameId,
            IEnumerable<ContentBlock> targetBlocks,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(gameId)) throw new ArgumentException("Game ID cannot be null or empty.", nameof(gameId));
            if (targetBlocks == null) throw new ArgumentNullException(nameof(targetBlocks));

            _logger.LogInformation("Initiating Block-Level Game Repair for game {GameId}...", gameId);
            bool allRepaired = true;

            foreach (var block in targetBlocks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 1. Verify block integrity
                bool isValid = await _storageService.VerifyBlockAsync(block.BlockId, cancellationToken);
                if (isValid)
                {
                    _logger.LogDebug("Block '{BlockId}' is healthy. No repair needed.", block.BlockId);
                    continue;
                }

                _logger.LogWarning("Corrupt block detected: '{BlockId}'. Purging block from local storage...", block.BlockId);

                // 2. Remove the corrupted block
                await _storageService.DeleteBlockAsync(block.BlockId, cancellationToken);

                // 3. Find a healthy peer having this block
                var peers = await _cacheManager.GetNodesWithBlockAsync(block.BlockId, cancellationToken);
                if (!peers.Any())
                {
                    _logger.LogError("Repair failed: No healthy online peers discovered containing block '{BlockId}'.", block.BlockId);
                    allRepaired = false;
                    continue;
                }

                var peer = peers.First();

                // 4. Download missing block from peer
                try
                {
                    _logger.LogInformation("Repairing block '{BlockId}' by fetching from peer node {NodeId}...", block.BlockId, peer.NodeId);
                    await _transferService.TransferBlockAsync(peer, block.BlockId, cancellationToken);

                    // 5. Re-verify the downloaded block
                    bool reVerified = await _storageService.VerifyBlockAsync(block.BlockId, cancellationToken);
                    if (reVerified)
                    {
                        _logger.LogInformation("Successfully repaired corrupt block '{BlockId}'.", block.BlockId);
                    }
                    else
                    {
                        _logger.LogError("Repair failed: Re-downloaded block '{BlockId}' is still invalid.", block.BlockId);
                        allRepaired = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception during repair transfer of block '{BlockId}' from peer node {NodeId}.", block.BlockId, peer.NodeId);
                    allRepaired = false;
                }
            }

            _logger.LogInformation("Block-Level Game Repair complete. Overall outcome: {Status}", allRepaired ? "SUCCESS" : "PARTIAL / FAILED");
            return allRepaired;
        }
    }
}
