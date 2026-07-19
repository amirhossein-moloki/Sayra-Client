using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.GameLibrary.Services;
using Sayra.Client.Launcher.Events;
using Sayra.Client.Launcher.Models;
using Sayra.Client.Launcher.Validation;

namespace Sayra.Client.Launcher.Services
{
    public class GameLauncherService : IGameLauncherService
    {
        private readonly IGameLibraryService _gameLibrary;
        private readonly IProcessMonitorService _processMonitor;
        private readonly ILauncherRecoveryService _recoveryService;
        private readonly ISessionStateProvider _sessionState;
        private readonly ILicenseValidator _licenseValidator;
        private readonly ILogger<GameLauncherService> _logger;

        public event EventHandler<GameLaunchingEventArgs>? GameLaunching;
        public event EventHandler<GameStartedEventArgs>? GameStarted;
        public event EventHandler<GameExitedEventArgs>? GameExited;
        public event EventHandler<GameCrashedEventArgs>? GameCrashed;
        public event EventHandler<GameRestartedEventArgs>? GameRestarted;
        public event EventHandler<GameKilledEventArgs>? GameKilled;
        public event EventHandler<LaunchFailedEventArgs>? LaunchFailed;

        public GameLauncherService(
            IGameLibraryService gameLibrary,
            IProcessMonitorService processMonitor,
            ILauncherRecoveryService recoveryService,
            ISessionStateProvider sessionState,
            ILicenseValidator licenseValidator,
            ILogger<GameLauncherService> logger)
        {
            _gameLibrary = gameLibrary;
            _processMonitor = processMonitor;
            _recoveryService = recoveryService;
            _sessionState = sessionState;
            _licenseValidator = licenseValidator;
            _logger = logger;
        }

        public async Task<bool> LaunchGameAsync(string gameId)
        {
            return await LaunchGameAsync(gameId, CancellationToken.None);
        }

        public async Task<bool> LaunchGameAsync(string gameId, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Attempting to launch game: '{GameId}'", gameId);

                // Check session active
                if (!_sessionState.IsSessionActive())
                {
                    _logger.LogWarning("Cannot launch game: Session is not active.");
                    RaiseLaunchFailed(gameId, "Session not active", "Session is not active.");
                    return false;
                }

                // Get game info
                var games = await _gameLibrary.GetGames();
                var game = games.FirstOrDefault(g => g.Id == gameId);
                if (game == null)
                {
                    _logger.LogWarning("Game with ID '{GameId}' not found in library.", gameId);
                    RaiseLaunchFailed(gameId, "Game not found", "Game not found in library.");
                    return false;
                }

                if (!game.Enabled)
                {
                    _logger.LogWarning("Game '{GameName}' is disabled.", game.Name);
                    RaiseLaunchFailed(gameId, game.Name, "Game is disabled.");
                    return false;
                }

                // Check license
                if (!_licenseValidator.IsLicenseValid(gameId))
                {
                    _logger.LogWarning("License validation failed for GameId: '{GameId}'", gameId);
                    RaiseLaunchFailed(gameId, game.Name, "License validation failed.");
                    return false;
                }

                // Validate executable
                if (string.IsNullOrWhiteSpace(game.ExecutablePath) || (!System.IO.File.Exists(game.ExecutablePath) && !OperatingSystem.IsLinux()))
                {
                    string error = $"Executable does not exist: {game.ExecutablePath}";
                    _logger.LogWarning(error);
                    RaiseLaunchFailed(gameId, game.Name, error);
                    return false;
                }

                // Raise GameLaunching event
                GameLaunching?.Invoke(this, new GameLaunchingEventArgs { GameId = gameId, Name = game.Name });

                // Launch process
                var psi = new ProcessStartInfo
                {
                    FileName = game.ExecutablePath,
                    Arguments = game.Arguments,
                    WorkingDirectory = string.IsNullOrWhiteSpace(game.WorkingDirectory) ? System.IO.Path.GetDirectoryName(game.ExecutablePath) : game.WorkingDirectory,
                    UseShellExecute = false
                };

                _logger.LogInformation("Starting process: '{Path}' with args: '{Args}'", psi.FileName, psi.Arguments);
                var process = Process.Start(psi);
                if (process == null)
                {
                    RaiseLaunchFailed(gameId, game.Name, "Failed to start process.");
                    return false;
                }

                // Register with process monitor
                var options = new LaunchOptions
                {
                    ExecutablePath = game.ExecutablePath,
                    Arguments = game.Arguments,
                    WorkingDirectory = game.WorkingDirectory
                };

                _processMonitor.RegisterProcess(gameId, process, options);

                // Raise GameStarted
                GameStarted?.Invoke(this, new GameStartedEventArgs { GameId = gameId, Name = game.Name, Pid = process.Id });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error launching game '{GameId}'", gameId);
                RaiseLaunchFailed(gameId, gameId, ex.Message);
                return false;
            }
        }

        public Task<bool> LaunchApplicationAsync(string path, string args, string workingDir, bool runAsAdmin, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Launching independent application: {Path} {Args}", path, args);

                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = args,
                    WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? System.IO.Path.GetDirectoryName(path) : workingDir,
                    UseShellExecute = runAsAdmin,
                    Verb = runAsAdmin ? "runas" : ""
                };

                var process = Process.Start(psi);
                if (process != null)
                {
                    _logger.LogInformation("Independent application started (PID: {Pid})", process.Id);
                    return Task.FromResult(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to launch application {Path}", path);
            }

            return Task.FromResult(false);
        }

        public async Task StopGameAsync(string gameId)
        {
            _logger.LogInformation("StopGameAsync requested for GameId: {GameId}", gameId);
            await KillGameAsync(gameId);
        }

        public async Task RestartGameAsync(string gameId)
        {
            _logger.LogInformation("RestartGameAsync requested for GameId: {GameId}", gameId);
            await KillGameAsync(gameId);
            await LaunchGameAsync(gameId);
        }

        public IEnumerable<ProcessStatistics> GetRunningGames()
        {
            return _processMonitor.GetRunningProcesses();
        }

        public ProcessStatistics? GetProcessStatistics(string gameId)
        {
            return _processMonitor.GetProcessStatistics(gameId);
        }

        public Task KillProcessAsync(int pid)
        {
            try
            {
                _logger.LogInformation("Request to kill process by PID: {Pid}", pid);
                var process = Process.GetProcessById(pid);
                process.Kill(true);
                _logger.LogInformation("Successfully killed process (PID: {Pid})", pid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill process by PID {Pid}", pid);
            }
            return Task.CompletedTask;
        }

        public Task KillProcessByNameAsync(string name)
        {
            try
            {
                _logger.LogInformation("Request to kill processes by Name: '{Name}'", name);
                var processes = Process.GetProcessesByName(name);
                foreach (var proc in processes)
                {
                    try
                    {
                        proc.Kill(true);
                        _logger.LogInformation("Successfully killed process '{Name}' (PID: {Pid})", name, proc.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to kill process '{Name}' (PID: {Pid})", name, proc.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to kill processes by name '{Name}'", name);
            }
            return Task.CompletedTask;
        }

        public Task KillGameAsync(string gameId)
        {
            try
            {
                _logger.LogInformation("Request to kill game: '{GameId}'", gameId);
                var stats = _processMonitor.GetProcessStatistics(gameId);
                if (stats != null && stats.IsRunning)
                {
                    try
                    {
                        var process = Process.GetProcessById(stats.Pid);
                        process.Kill(true);
                        _logger.LogInformation("Successfully killed process PID {Pid} for game '{GameId}'", stats.Pid, gameId);
                        GameKilled?.Invoke(this, new GameKilledEventArgs { GameId = gameId, Name = stats.Name, Pid = stats.Pid });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to kill process PID {Pid}", stats.Pid);
                    }
                }
                _processMonitor.UnregisterProcess(gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error killing game '{GameId}'", gameId);
            }

            return Task.CompletedTask;
        }

        public void RaiseGameCrashed(string gameId, string name, int exitCode, string reason)
        {
            GameCrashed?.Invoke(this, new GameCrashedEventArgs { GameId = gameId, Name = name, ExitCode = exitCode, Reason = reason });
        }

        public void RaiseGameExited(string gameId, string name, int exitCode, TimeSpan duration)
        {
            GameExited?.Invoke(this, new GameExitedEventArgs { GameId = gameId, Name = name, ExitCode = exitCode, Duration = duration });
        }

        public void RaiseGameRestarted(string gameId, string name, int retryCount)
        {
            GameRestarted?.Invoke(this, new GameRestartedEventArgs { GameId = gameId, Name = name, RetryCount = retryCount });
        }

        private void RaiseLaunchFailed(string gameId, string name, string reason)
        {
            LaunchFailed?.Invoke(this, new LaunchFailedEventArgs { GameId = gameId, Name = name, Reason = reason });
        }
    }
}
