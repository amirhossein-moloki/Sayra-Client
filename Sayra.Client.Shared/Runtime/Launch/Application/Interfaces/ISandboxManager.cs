using System.Threading.Tasks;

namespace Sayra.Client.Shared.Runtime.Launch.Application.Interfaces
{
    public interface ISandboxManager
    {
        /// <summary>
        /// Prepares the isolated sandbox directories (SaveData, Temp, Cache) for a given game.
        /// Throws exceptions on critical failures, enabling safe rollback.
        /// </summary>
        Task PrepareSandboxAsync(string gameId, string sandboxPath);

        /// <summary>
        /// Cleans up isolated sandbox directories idempotently.
        /// </summary>
        Task CleanupSandboxAsync(string gameId, string sandboxPath);
    }
}
