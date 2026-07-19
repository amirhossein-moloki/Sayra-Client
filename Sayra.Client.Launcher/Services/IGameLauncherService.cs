using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Launcher.Events;
using Sayra.Client.Launcher.Models;

namespace Sayra.Client.Launcher.Services
{
    public interface IGameLauncherService
    {
        event EventHandler<GameLaunchingEventArgs> GameLaunching;
        event EventHandler<GameStartedEventArgs> GameStarted;
        event EventHandler<GameExitedEventArgs> GameExited;
        event EventHandler<GameCrashedEventArgs> GameCrashed;
        event EventHandler<GameRestartedEventArgs> GameRestarted;
        event EventHandler<GameKilledEventArgs> GameKilled;
        event EventHandler<LaunchFailedEventArgs> LaunchFailed;

        Task<bool> LaunchGameAsync(string gameId);
        Task<bool> LaunchGameAsync(string gameId, CancellationToken cancellationToken);
        Task<bool> LaunchApplicationAsync(string path, string args, string workingDir, bool runAsAdmin, CancellationToken cancellationToken = default);
        Task StopGameAsync(string gameId);
        Task KillGameAsync(string gameId);
        Task RestartGameAsync(string gameId);
        IEnumerable<ProcessStatistics> GetRunningGames();
        ProcessStatistics? GetProcessStatistics(string gameId);
        Task KillProcessAsync(int pid);
        Task KillProcessByNameAsync(string name);

        void RaiseGameCrashed(string gameId, string name, int exitCode, string reason);
        void RaiseGameExited(string gameId, string name, int exitCode, TimeSpan duration);
        void RaiseGameRestarted(string gameId, string name, int retryCount);
    }
}
