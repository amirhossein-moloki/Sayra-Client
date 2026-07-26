using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Runtime.Launch.Application.Interfaces
{
    /// <summary>
    /// Contract for Application Registry Isolation to configure game-specific virtual registry trees.
    /// </summary>
    public interface IRegistryVirtualizationManager
    {
        /// <summary>
        /// Prepares/Virtualizes the game-specific registry keys under a session-isolated sandboxed registry path.
        /// </summary>
        Task PrepareRegistryAsync(Guid sessionId, string gameId, Dictionary<string, string> virtualKeys);

        /// <summary>
        /// Cleans up virtualized game-specific registry keys for the given session.
        /// </summary>
        Task CleanupRegistryAsync(Guid sessionId, string gameId, Dictionary<string, string> virtualKeys);
    }
}
