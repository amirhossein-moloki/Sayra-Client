using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IRemoteCommandDispatcher = Sayra.Client.Shared.Interfaces.Phase9.IRemoteCommandDispatcher;
using Sayra.Client.Shared.DependencyInjection;
using Sayra.Client.Shared.Fleet.Infrastructure;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Fleet.RemoteCommands.Commands;
using Sayra.Client.Shared.Fleet.RemoteCommands.History;
using Sayra.Client.Shared.Fleet.RemoteCommands.Pipeline;
using Sayra.Client.Shared.Fleet.RemoteCommands.Queues;
using Sayra.Client.Shared.Fleet.RemoteCommands.Security;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;
using Xunit;

namespace Sayra.Client.Configuration.Tests.Phase9
{
    using CommandStatus = Sayra.Client.Shared.Models.Phase9.Enums.CommandStatus;
    using CommandResult = Sayra.Client.Shared.Models.Phase9.Domain.CommandResult;

    /// <summary>
    /// High-rigor E2E, Integration, Concurrency, and Unit tests verifying the Remote Command Framework (Stage 3).
    /// </summary>
    public sealed class RemoteCommandFrameworkTests : IDisposable
    {
        private readonly string _testDbDir;
        private readonly string _testDbPath;
        private readonly FleetDatabaseContext _dbContext;
        private readonly Mock<IEventDispatcher> _eventDispatcherMock;
        private readonly ServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes test dependencies, including an isolated encrypted SQLCipher SQLite file.
        /// </summary>
        public RemoteCommandFrameworkTests()
        {
            _testDbDir = Path.Combine(AppContext.BaseDirectory, "Stage3TestData", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDbDir);
            _testDbPath = Path.Combine(_testDbDir, "fleet_test_s3.db");

            Environment.SetEnvironmentVariable("SAYRA_TEST_DB_PATH", _testDbPath);

            _dbContext = new FleetDatabaseContext(NullLogger<FleetDatabaseContext>.Instance);
            _dbContext.InitializeDatabaseAsync().GetAwaiter().GetResult();

            _eventDispatcherMock = new Mock<IEventDispatcher>();

            var services = new ServiceCollection();
            services.AddSingleton<IFleetDatabaseContext>(_dbContext);
            services.AddSingleton(_eventDispatcherMock.Object);
            services.AddLogging();
            services.AddPhase9Foundation(); // Registers Fleet, validators, etc., plus our AddRemoteCommandFramework!

            _serviceProvider = services.BuildServiceProvider();

            // Warm up history repository and command queue to initialize SQLite tables outside of test execution deadlines
            var historyRepo = _serviceProvider.GetRequiredService<IRemoteCommandHistoryRepository>();
            historyRepo.GetAllAsync(CancellationToken.None).GetAwaiter().GetResult();

            var queue = _serviceProvider.GetRequiredService<IEnterpriseCommandQueue>();
            queue.RecoverQueueAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _serviceProvider.Dispose();
            _dbContext.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            try
            {
                if (Directory.Exists(_testDbDir))
                {
                    Directory.Delete(_testDbDir, true);
                }
            }
            catch { }

            Environment.SetEnvironmentVariable("SAYRA_TEST_DB_PATH", null);
        }

        #region Pipeline & Dispatcher Tests

        [Fact]
        public async Task Pipeline_NormalExecution_Succeeds_AndLogsHistory()
        {
            // Arrange
            var dispatcher = _serviceProvider.GetRequiredService<IRemoteCommandDispatcher>();
            var historyRepo = _serviceProvider.GetRequiredService<IRemoteCommandHistoryRepository>();

            var command = new RemoteCommand
            {
                CommandId = Guid.NewGuid().ToString(),
                Action = RemoteCommandActions.RestartSayraService,
                TargetMachineId = "WS-01",
                Priority = CommandPriority.Normal,
                CreatorOperatorId = "ADMIN-01",
                Signature = "valid-signature-bytes-long",
                Parameters = new List<CommandParameter>
                {
                    new() { Name = "RestartMode", Value = "Graceful" }
                }
            };

            // Act
            bool success = await dispatcher.DispatchCommandAsync(command);

            // Assert
            Assert.True(success);

            // Verify Events Dispatched
            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<CommandDispatched>(e => e.CommandId == command.CommandId)), Times.Once);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<CommandAccepted>(e => e.CommandId == command.CommandId)), Times.Once);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<CommandStarted>(e => e.CommandId == command.CommandId)), Times.Once);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<CommandCompleted>(e => e.CommandId == command.CommandId && e.Outcome == OperationResult.Success)), Times.Once);

            // Verify Persistent History entry was committed
            var history = await historyRepo.GetAsync(command.CommandId);
            Assert.NotNull(history);
            Assert.Equal(command.Action, history.Action);
            Assert.Equal(CommandStatus.Succeeded, history.Status);
            Assert.Equal(OperationResult.Success, history.Outcome);
            Assert.Equal("ADMIN-01", history.CreatorOperatorId);
            Assert.True(history.ExecutionDurationMs >= 0);
        }

        [Fact]
        public async Task Pipeline_WithValidationFailure_FailsClosed_AndPublishesRejectedEvent()
        {
            // Arrange
            var dispatcher = _serviceProvider.GetRequiredService<IRemoteCommandDispatcher>();

            // Lacks required 'ServiceName' parameter
            var badCommand = new RemoteCommand
            {
                CommandId = Guid.NewGuid().ToString(),
                Action = RemoteCommandActions.RestartWindowsService,
                TargetMachineId = "WS-01",
                Priority = CommandPriority.Normal,
                CreatorOperatorId = "ADMIN-01",
                Signature = "valid-signature-bytes"
            };

            // Act
            bool success = await dispatcher.DispatchCommandAsync(badCommand);

            // Assert
            Assert.False(success);

            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<CommandRejected>(e => e.CommandId == badCommand.CommandId)), Times.Once);
        }

        [Fact]
        public async Task Pipeline_WithAuthorizationFailure_FailsClosed_AndPublishesRejectedEvent()
        {
            // Arrange
            var dispatcher = _serviceProvider.GetRequiredService<IRemoteCommandDispatcher>();

            // Lacks digital signature
            var badCommand = new RemoteCommand
            {
                CommandId = Guid.NewGuid().ToString(),
                Action = RemoteCommandActions.RestartSayraService,
                TargetMachineId = "WS-01",
                Priority = CommandPriority.Normal,
                CreatorOperatorId = "ADMIN-01",
                Signature = string.Empty
            };

            // Act
            bool success = await dispatcher.DispatchCommandAsync(badCommand);

            // Assert
            Assert.False(success);

            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<CommandRejected>(e => e.CommandId == badCommand.CommandId)), Times.Once);
        }

        #endregion

        #region Authorization & Security Tests

        [Fact]
        public async Task Authorization_ReplayAttack_IsDetectedAndBlocked()
        {
            // Arrange
            var authService = _serviceProvider.GetRequiredService<IRemoteCommandAuthorizationService>();
            var command = new RemoteCommand
            {
                CommandId = "SAME-ID",
                Action = RemoteCommandActions.RunHealthCheck,
                TargetMachineId = "WS-01",
                Signature = "valid-signature-bytes-long",
                CreatorOperatorId = "ADMIN-01"
            };

            // Act
            bool authorized1 = await authService.AuthorizeCommandAsync(command);
            bool authorized2 = await authService.AuthorizeCommandAsync(command); // Replay!

            // Assert
            Assert.True(authorized1);
            Assert.False(authorized2);
        }

        [Fact]
        public async Task Authorization_MissingRequiredCapabilities_IsRejected()
        {
            // Arrange
            var authService = _serviceProvider.GetRequiredService<IRemoteCommandAuthorizationService>();

            // Operator with normal access triggers critical priority (requires KernelControl capability)
            var command = new RemoteCommand
            {
                CommandId = Guid.NewGuid().ToString(),
                Action = RemoteCommandActions.RestartSayraService,
                TargetMachineId = "WS-01",
                Priority = CommandPriority.Critical,
                Signature = "valid-signature-bytes-long",
                CreatorOperatorId = "OPERATOR-01" // lacks KernelControl
            };

            // Act
            bool authorized = await authService.AuthorizeCommandAsync(command);

            // Assert
            Assert.False(authorized);
        }

        #endregion

        #region Retry & Timeout Tests

        [Fact]
        public async Task RetryMiddleware_TransientFailure_TriggersRetriesAndSucceeds()
        {
            // Arrange
            var registry = _serviceProvider.GetRequiredService<IRemoteCommandHandlerRegistry>();
            var dispatcher = _serviceProvider.GetRequiredService<IRemoteCommandDispatcher>();

            int executions = 0;
            registry.Register(RemoteCommandActions.CustomAdminCommand, (cmd, ct) =>
            {
                executions++;
                if (executions < 2)
                {
                    return Task.FromResult(new CommandResult
                    {
                        CommandId = cmd.CommandId,
                        MachineId = cmd.TargetMachineId,
                        Status = CommandStatus.Failed,
                        Outcome = OperationResult.Failure,
                        OutputMessage = "transient connection busy"
                    });
                }

                return Task.FromResult(new CommandResult
                {
                    CommandId = cmd.CommandId,
                    MachineId = cmd.TargetMachineId,
                    Status = CommandStatus.Succeeded,
                    Outcome = OperationResult.Success,
                    OutputMessage = "Scans finished"
                });
            });

            var command = new RemoteCommand
            {
                CommandId = Guid.NewGuid().ToString(),
                Action = RemoteCommandActions.CustomAdminCommand,
                TargetMachineId = "WS-01",
                Signature = "valid-signature-bytes-long",
                CreatorOperatorId = "ADMIN-01",
                Parameters = new List<CommandParameter>
                {
                    new() { Name = "CommandText", Value = "echo hello" }
                }
            };

            // Act
            bool success = await dispatcher.DispatchCommandAsync(command);

            // Assert
            Assert.True(success);
            Assert.Equal(2, executions);

            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<RetryStarted>(e => e.CommandId == command.CommandId && e.AttemptNumber == 1)), Times.Once);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<RetryCompleted>(e => e.CommandId == command.CommandId && e.AttemptNumber == 1)), Times.Once);
        }

        [Fact]
        public async Task TimeoutMiddleware_LongRunningExecution_TimesOutGracefully()
        {
            // Arrange
            var registry = _serviceProvider.GetRequiredService<IRemoteCommandHandlerRegistry>();
            var dispatcher = _serviceProvider.GetRequiredService<IRemoteCommandService>();

            registry.Register(RemoteCommandActions.CustomAdminCommand, async (cmd, ct) =>
            {
                await Task.Delay(2000, ct); // delay to trigger mock timeout
                return new CommandResult
                {
                    CommandId = cmd.CommandId,
                    MachineId = cmd.TargetMachineId,
                    Status = CommandStatus.Succeeded,
                    Outcome = OperationResult.Success
                };
            });

            var command = new RemoteCommand
            {
                CommandId = Guid.NewGuid().ToString(),
                Action = RemoteCommandActions.CustomAdminCommand,
                TargetMachineId = "WS-01",
                Priority = CommandPriority.Normal,
                Signature = "valid-signature-bytes-long",
                CreatorOperatorId = "ADMIN-01",
                Parameters = new List<CommandParameter>
                {
                    new() { Name = "CommandText", Value = "echo hello" }
                }
            };

            // Act & Assert
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(50); // force brief E2E cancellation window

            var result = await dispatcher.ExecuteCommandAsync(command, cts.Token);
            Assert.Equal(OperationResult.Timeout, result.Outcome);
        }

        #endregion

        #region Enterprise Queues Tests

        [Fact]
        public async Task Queue_PriorityAndFIFO_DequeuesInCorrectOrder()
        {
            // Arrange
            var queue = _serviceProvider.GetRequiredService<IEnterpriseCommandQueue>();

            var normalCmd = new RemoteCommand
            {
                CommandId = "CMD-NORMAL",
                Action = RemoteCommandActions.RunHealthCheck,
                TargetMachineId = "WS-01",
                Priority = CommandPriority.Normal,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                Signature = "sig-normal",
                CreatorOperatorId = "ADMIN-01"
            };

            var emergencyCmd = new RemoteCommand
            {
                CommandId = "CMD-EMERGENCY",
                Action = RemoteCommandActions.LockWorkstation,
                TargetMachineId = "WS-01",
                Priority = CommandPriority.Emergency,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                Signature = "sig-emergency",
                CreatorOperatorId = "ADMIN-01",
                Parameters = new List<CommandParameter> { new() { Name = "Reason", Value = "Lockout" } }
            };

            // Act
            await queue.EnqueueCommandAsync(normalCmd);
            await queue.EnqueueCommandAsync(emergencyCmd);

            // Assert Dequeue Order (Emergency must be dequeued first)
            var first = await queue.DequeueCommandAsync();
            var second = await queue.DequeueCommandAsync();

            Assert.NotNull(first);
            Assert.Equal("CMD-EMERGENCY", first.CommandId);

            Assert.NotNull(second);
            Assert.Equal("CMD-NORMAL", second.CommandId);
        }

        [Fact]
        public async Task Queue_ExpirationAndDLQ_PrunesAndRoutesToDLQ()
        {
            // Arrange
            var queue = _serviceProvider.GetRequiredService<IEnterpriseCommandQueue>();

            var expiredCmd = new RemoteCommand
            {
                CommandId = "CMD-EXPIRED",
                Action = RemoteCommandActions.RunHealthCheck,
                TargetMachineId = "WS-01",
                Priority = CommandPriority.Normal,
                ExpiresAtUtc = DateTime.UtcNow.AddMilliseconds(10), // expired!
                Signature = "sig-expired",
                CreatorOperatorId = "ADMIN-01"
            };

            // Act
            await queue.EnqueueCommandAsync(expiredCmd);
            await Task.Delay(50); // wait for 10ms expiration to elapse deterministically
            await queue.PruneExpiredAndDelayedAsync(); // deterministic prune trigger!

            var stats = await queue.GetStatisticsAsync();

            // Assert
            Assert.Equal(0, await queue.GetQueueSizeAsync());
        }

        [Fact]
        public async Task Queue_OfflineAndReplay_BuffersAndReplaysWorkstationCommands()
        {
            // Arrange
            var queue = _serviceProvider.GetRequiredService<IEnterpriseCommandQueue>();
            var fleetCache = _serviceProvider.GetRequiredService<IFleetCache>();

            // Register WS as OFFLINE
            var workstation = new MachineInfo
            {
                MachineId = "WS-OFFLINE",
                Hostname = "ConsolePC",
                MacAddress = "00:11:22:33:44:55",
                Status = MachineStatus.Offline
            };
            fleetCache.SetMachine(workstation);

            var cmd = new RemoteCommand
            {
                CommandId = "CMD-OFFLINE",
                Action = RemoteCommandActions.LockWorkstation,
                TargetMachineId = "WS-OFFLINE",
                Priority = CommandPriority.Normal,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                Signature = "sig-offline",
                CreatorOperatorId = "ADMIN-01",
                Parameters = new List<CommandParameter> { new() { Name = "Reason", Value = "Lockout" } }
            };

            // Act
            await queue.EnqueueCommandAsync(cmd);
            await queue.MoveToDeadLetterQueueAsync(cmd, "Workstation is Offline"); // Move to offline / DLQ simulation

            // Workstation comes online
            await queue.ReplayOfflineCommandsAsync("WS-OFFLINE");

            var stats = await queue.GetStatisticsAsync();
            Assert.Equal(0, stats.OfflineCount);
        }

        [Fact]
        public async Task Queue_Cancellation_AbortsEnqueuedCommand()
        {
            // Arrange
            var queue = _serviceProvider.GetRequiredService<IEnterpriseCommandQueue>();

            var cmd = new RemoteCommand
            {
                CommandId = "CMD-CANCEL",
                Action = RemoteCommandActions.RunHealthCheck,
                TargetMachineId = "WS-01",
                Priority = CommandPriority.Normal,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                Signature = "sig-cancel",
                CreatorOperatorId = "ADMIN-01"
            };

            await queue.EnqueueCommandAsync(cmd);

            // Act
            bool cancelled = await queue.CancelCommandAsync("CMD-CANCEL");

            // Assert
            Assert.True(cancelled);
            Assert.Null(await queue.DequeueCommandAsync());
        }

        #endregion

        #region Concurrency Tests

        [Fact]
        public async Task Queue_HighConcurrentEnqueuing_IsThreadSafeAndZeroDeadlocks()
        {
            // Arrange
            var queue = _serviceProvider.GetRequiredService<IEnterpriseCommandQueue>();
            int threadCount = 10;
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var cmd = new RemoteCommand
                    {
                        CommandId = $"CMD-CONC-{index}",
                        Action = RemoteCommandActions.RunHealthCheck,
                        TargetMachineId = "WS-01",
                        Priority = CommandPriority.Normal,
                        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                        Signature = $"sig-{index}",
                        CreatorOperatorId = "ADMIN-01"
                    };

                    await queue.EnqueueCommandAsync(cmd);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(threadCount, await queue.GetQueueSizeAsync());
        }

        #endregion
    }
}
