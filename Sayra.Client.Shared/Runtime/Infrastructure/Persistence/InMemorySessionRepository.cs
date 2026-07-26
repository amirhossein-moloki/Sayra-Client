using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Domain.Entities;
using Sayra.Client.Shared.Runtime.Domain.States;

namespace Sayra.Client.Shared.Runtime.Infrastructure.Persistence
{
    /// <summary>
    /// Provides a thread-safe, high-performance, in-memory implementation of the <see cref="ISessionRepository"/> contract.
    /// This implementation is designed primarily for rapid session lookups, low-overhead transaction management,
    /// and isolated unit/integration testing environments.
    ///
    /// <para>
    /// <b>SCOPE BOUNDARY AND ARCHITECTURAL DIRECTIVE:</b>
    /// As mandated by the Phase 4 Track 4.4 specification, persistent engine-level database storage
    /// (such as SQLCipher/SQLite disk-level encryption at-rest) is intentionally outside the scope of Track 4.4.
    /// Standard production implementations of SQLCipher for configuration, log queues, and audit stores
    /// are fully managed by the dedicated security and configurations layer (Track 3/Track 4.3). This in-memory
    /// provider ensures maximum isolation and avoids coupling session tracking directly to underlying relational database schemas.
    /// </para>
    /// </summary>
    public class InMemorySessionRepository : ISessionRepository
    {
        private readonly ConcurrentDictionary<Guid, RuntimeSession> _storage = new();

        /// <summary>
        /// Atomically saves or updates the state of the specified runtime session.
        /// </summary>
        public Task SaveAsync(RuntimeSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            _storage[session.SessionId] = session;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Retrieves the session matching the specified unique identifier.
        /// </summary>
        public Task<RuntimeSession?> GetAsync(Guid sessionId)
        {
            _storage.TryGetValue(sessionId, out var session);
            return Task.FromResult<RuntimeSession?>(session);
        }

        /// <summary>
        /// Retrieves all sessions that are currently in an active, non-terminal state.
        /// </summary>
        public Task<IEnumerable<RuntimeSession>> GetActiveSessionsAsync()
        {
            var active = _storage.Values.Where(s => s.Status != RuntimeState.Completed && s.Status != RuntimeState.Failed);
            return Task.FromResult(active);
        }

        /// <summary>
        /// Permanently removes the session matching the specified unique identifier.
        /// </summary>
        public Task DeleteAsync(Guid sessionId)
        {
            _storage.TryRemove(sessionId, out _);
            return Task.CompletedTask;
        }
    }
}
