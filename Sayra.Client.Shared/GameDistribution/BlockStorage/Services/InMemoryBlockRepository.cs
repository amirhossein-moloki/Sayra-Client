using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Interfaces;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Models;

namespace Sayra.Client.Shared.GameDistribution.BlockStorage.Services
{
    public class InMemoryBlockRepository : IBlockRepository
    {
        private readonly ConcurrentDictionary<string, ContentBlock> _blocks = new();

        public Task SaveAsync(ContentBlock block, CancellationToken cancellationToken = default)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            _blocks[block.BlockId] = block;
            return Task.CompletedTask;
        }

        public Task<ContentBlock?> GetAsync(string blockId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(blockId)) return Task.FromResult<ContentBlock?>(null);
            _blocks.TryGetValue(blockId, out var block);
            return Task.FromResult(block);
        }

        public Task<IEnumerable<ContentBlock>> GetByGameAsync(string gameId, CancellationToken cancellationToken = default)
        {
            var matched = _blocks.Values.Where(b => b.GameId == gameId).ToList();
            return Task.FromResult<IEnumerable<ContentBlock>>(matched);
        }

        public Task<IEnumerable<ContentBlock>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<ContentBlock>>(_blocks.Values.ToList());
        }

        public Task DeleteAsync(string blockId, CancellationToken cancellationToken = default)
        {
            _blocks.TryRemove(blockId, out _);
            return Task.CompletedTask;
        }
    }
}
