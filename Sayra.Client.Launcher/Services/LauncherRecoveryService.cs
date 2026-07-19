using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Launcher.Models;

namespace Sayra.Client.Launcher.Services
{
    public interface ILauncherRecoveryService
    {
        Task HandleGameCrashAsync(string gameId, LaunchOptions options, int exitCode);
    }

    public class LauncherRecoveryService : ILauncherRecoveryService
    {
        private readonly ILogger<LauncherRecoveryService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, int> _retryCounts = new();
        private const int MaxRecoveryRetries = 3;

        public LauncherRecoveryService(ILogger<LauncherRecoveryService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task HandleGameCrashAsync(string gameId, LaunchOptions options, int exitCode)
        {
            try
            {
                int attempts = _retryCounts.AddOrUpdate(gameId, 1, (_, count) => count + 1);
                _logger.LogWarning("Game '{GameId}' crashed with exit code {ExitCode}. Recovery attempt {Attempts}/{Max}",
                    gameId, exitCode, attempts, MaxRecoveryRetries);

                if (attempts <= MaxRecoveryRetries)
                {
                    var launcher = _serviceProvider.GetService<IGameLauncherService>();
                    if (launcher is GameLauncherService concreteLauncher)
                    {
                        concreteLauncher.RaiseGameRestarted(gameId, gameId, attempts);
                        _logger.LogInformation("Triggering relaunch for game '{GameId}'...", gameId);
                        await launcher.LaunchGameAsync(gameId);
                    }
                }
                else
                {
                    _logger.LogError("Max recovery attempts ({Max}) reached for game '{GameId}'. Aborting recovery.",
                        MaxRecoveryRetries, gameId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during game recovery for game '{GameId}'", gameId);
            }
        }
    }
}
