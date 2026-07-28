using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Manages update package cache tracking, validation, eviction, and integrity checks.
    /// </summary>
    public interface ICacheManager
    {
        Task<CacheEntry> AddOrUpdateAsync(string key, string filePath, string entryType, string version, long sizeBytes, string sha256Hash, DateTime? expiresAt = null, CancellationToken cancellationToken = default);
        Task<CacheEntry?> GetAsync(string key, CancellationToken cancellationToken = default);
        Task<IEnumerable<CacheEntry>> GetAllAsync(CancellationToken cancellationToken = default);
        Task EvictAsync(string key, CancellationToken cancellationToken = default);
        Task EvictLruAsync(CancellationToken cancellationToken = default);
        Task CleanExpiredAsync(CancellationToken cancellationToken = default);
        Task ClearInvalidAndFailedAsync(CancellationToken cancellationToken = default);
        Task ValidateIntegrityAsync(CancellationToken cancellationToken = default);
    }
}
