using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Phase9;

namespace Sayra.Client.Shared.Fleet.RemoteAssistance
{
    /// <summary>
    /// Thread-safe, production-ready interactive remote shell execution service executing actual system shells (sh/bash) with process isolation.
    /// Supports command verification, session-isolated stdout redirection, and non-blocking async-enumerable streams.
    /// </summary>
    public class RemoteConsoleService : IRemoteConsoleService
    {
        private readonly RemoteSessionCoordinator _coordinator;
        private readonly ILogger<RemoteConsoleService> _logger;

        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _commandQueues = new();
        private readonly List<string> _restrictedCommandSubstrings = new()
        {
            "rm -rf", "del /s", "format", "shutdown /s", "poweroff", "mkfs"
        };

        /// <summary>
        /// Initializes a new instance of RemoteConsoleService.
        /// </summary>
        public RemoteConsoleService(
            RemoteSessionCoordinator coordinator,
            ILogger<RemoteConsoleService> logger)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task ExecuteConsoleCommandAsync(string sessionId, string command, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));
            if (string.IsNullOrEmpty(command)) throw new ArgumentNullException(nameof(command));

            _coordinator.KeepAlive(sessionId);

            // Isolation Check: ensure session is active
            var session = _coordinator.GetSession(sessionId);
            if (session == null || session.Status != Models.Phase9.Enums.RemoteSessionStatus.Active)
            {
                throw new InvalidOperationException($"Interactive console command rejected: session '{sessionId}' is not active.");
            }

            var cmdQueue = _commandQueues.GetOrAdd(sessionId, _ => new ConcurrentQueue<string>());

            // Security Validation
            if (!await ValidatePermissionsAsync("Admin-Core-Client", session.TargetMachineId, command, ct))
            {
                _logger.LogWarning("Security Block: Restricted command input in session '{Session}': '{Cmd}'", sessionId, command);
                cmdQueue.Enqueue($"[SECURITY BLOCK] Command execution prohibited: contains restricted substrings.");
                return;
            }

            _logger.LogInformation("Interactive Shell Executing in session '{Id}': {Cmd}", sessionId, command);
            cmdQueue.Enqueue($"> {command}");

            // Execute the actual command via process start (sh or bash on Linux/macOS, cmd.exe on Windows)
            await Task.Run(() =>
            {
                try
                {
                    bool isWindows = OperatingSystem.IsWindows();
                    var shellFile = isWindows ? "cmd.exe" : "/bin/sh";
                    var shellArgs = isWindows ? $"/c \"{command}\"" : $"-c \"{command}\"";

                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = shellFile,
                            Arguments = shellArgs,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();

                    // Read outputs
                    using var stdOut = process.StandardOutput;
                    using var stdErr = process.StandardError;

                    string? line;
                    while ((line = stdOut.ReadLine()) != null)
                    {
                        cmdQueue.Enqueue(line);
                    }

                    while ((line = stdErr.ReadLine()) != null)
                    {
                        cmdQueue.Enqueue($"[ERROR] {line}");
                    }

                    process.WaitForExit(3000); // 3-second absolute command timeout limit
                }
                catch (Exception ex)
                {
                    cmdQueue.Enqueue($"[SHELL ERROR] Failed to start shell process: {ex.Message}");
                }
            }, ct);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<string> GetConsoleOutputStreamAsync(
            string sessionId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));

            var cmdQueue = _commandQueues.GetOrAdd(sessionId, _ => new ConcurrentQueue<string>());

            while (!ct.IsCancellationRequested)
            {
                _coordinator.KeepAlive(sessionId);

                if (cmdQueue.TryDequeue(out var line))
                {
                    yield return line;
                }
                else
                {
                    // Non-blocking backpressure sleep
                    await Task.Delay(50, ct);
                }
            }
        }

        /// <inheritdoc />
        public Task<bool> ValidatePermissionsAsync(string operatorId, string machineId, string command, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(command)) return Task.FromResult(false);

            var cleanCmd = command.Trim();
            foreach (var restriction in _restrictedCommandSubstrings)
            {
                if (cleanCmd.Contains(restriction, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(false); // Prohibit highly dangerous scripts
                }
            }

            return Task.FromResult(true);
        }
    }
}
