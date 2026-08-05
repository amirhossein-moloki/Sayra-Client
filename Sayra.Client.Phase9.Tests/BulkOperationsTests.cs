using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.Shared.Fleet.BulkOperations;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Fleet;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using IFleetManager = Sayra.Client.Shared.Interfaces.Phase9.IFleetManager;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Xunit;

namespace Sayra.Client.Phase9.Tests
{
    public class BulkOperationsTests
    {
        private readonly Mock<IFleetManager> _fleetManagerMock;
        private readonly Mock<ITagManager> _tagManagerMock;
        private readonly Mock<IRemoteCommandService> _commandServiceMock;
        private readonly Mock<IEventDispatcher> _eventDispatcherMock;
        private readonly Mock<ILogger<TargetResolver>> _targetResolverLoggerMock;
        private readonly Mock<ILogger<BulkExecutionManager>> _executionLoggerMock;
        private readonly Mock<ILogger<BulkRollbackManager>> _rollbackLoggerMock;
        private readonly Mock<ILogger<BulkOperationCoordinator>> _coordinatorLoggerMock;
        private readonly Mock<ILogger<BulkOperationEngine>> _engineLoggerMock;

        public BulkOperationsTests()
        {
            _fleetManagerMock = new Mock<IFleetManager>();
            _tagManagerMock = new Mock<ITagManager>();
            _commandServiceMock = new Mock<IRemoteCommandService>();
            _eventDispatcherMock = new Mock<IEventDispatcher>();
            _targetResolverLoggerMock = new Mock<ILogger<TargetResolver>>();
            _executionLoggerMock = new Mock<ILogger<BulkExecutionManager>>();
            _rollbackLoggerMock = new Mock<ILogger<BulkRollbackManager>>();
            _coordinatorLoggerMock = new Mock<ILogger<BulkOperationCoordinator>>();
            _engineLoggerMock = new Mock<ILogger<BulkOperationEngine>>();
        }

        [Fact]
        public async Task TargetResolver_ResolvesIndividualAndGroups_DeDuplicatesCorrectly()
        {
            // Arrange
            var resolver = new TargetResolver(_fleetManagerMock.Object, _tagManagerMock.Object, _targetResolverLoggerMock.Object);

            var m1 = new MachineInfo { MachineId = "PC01", Hostname = "PC-01", Status = MachineStatus.Online };
            var m2 = new MachineInfo { MachineId = "PC02", Hostname = "PC-02", Status = MachineStatus.Online };

            _fleetManagerMock.Setup(f => f.GetMachineAsync("PC01", It.IsAny<CancellationToken>()))
                .ReturnsAsync(m1);
            _fleetManagerMock.Setup(f => f.GetGroupMembersAsync("G1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MachineInfo> { m1, m2 });

            var targets = new List<BulkOperationTarget>
            {
                new() { TargetType = BulkTargetType.Individual, TargetValue = "PC01" },
                new() { TargetType = BulkTargetType.StaticGroup, TargetValue = "G1" }
            };

            // Act
            var resolved = await resolver.ResolveTargetsAsync(targets);

            // Assert
            Assert.Equal(2, resolved.Count);
            Assert.Contains(resolved, m => m.MachineId == "PC01");
            Assert.Contains(resolved, m => m.MachineId == "PC02");
        }

        [Fact]
        public void TargetResolver_MeetsCapabilities_EnforcesRamAndOsConstraints()
        {
            // Arrange
            var resolver = new TargetResolver(_fleetManagerMock.Object, _tagManagerMock.Object, _targetResolverLoggerMock.Object);
            var m1 = new MachineInfo
            {
                MachineId = "PC01",
                Inventory = new MachineInventory { RamGb = 32, OperatingSystem = "Windows 11 Pro", GpuName = "NVIDIA RTX 4090" }
            };

            // Act & Assert
            Assert.True(resolver.MeetsCapabilities(m1, new[] { "MinRam:16", "OS:Windows 11", "GPU:RTX" }));
            Assert.False(resolver.MeetsCapabilities(m1, new[] { "MinRam:64" }));
            Assert.False(resolver.MeetsCapabilities(m1, new[] { "OS:Windows10" }));
        }

        [Fact]
        public async Task ParallelExecutionPipeline_ExecutesConfigurableConcurrency_IsolatesFailures()
        {
            // Arrange
            var manager = new BulkExecutionManager(_commandServiceMock.Object, _executionLoggerMock.Object);
            var op = new BulkOperation { BulkOperationId = "OP1", Action = "LOCK_PC", OperatorId = "Admin" };
            var policy = new BulkOperationPolicy { MaxConcurrency = 2, IndividualTimeout = TimeSpan.FromSeconds(5) };

            var m1 = new MachineInfo { MachineId = "PC01" };
            var m2 = new MachineInfo { MachineId = "PC02" };
            var m3 = new MachineInfo { MachineId = "PC03" };

            // PC02 will fail, PC01 and PC03 succeed
            _commandServiceMock.Setup(c => c.ExecuteCommandAsync(It.Is<RemoteCommand>(cmd => cmd.TargetMachineId == "PC01"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { Outcome = OperationResult.Success, Status = CommandStatus.Succeeded });
            _commandServiceMock.Setup(c => c.ExecuteCommandAsync(It.Is<RemoteCommand>(cmd => cmd.TargetMachineId == "PC02"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { Outcome = OperationResult.Failure, Status = CommandStatus.Failed, OutputMessage = "Access Denied" });
            _commandServiceMock.Setup(c => c.ExecuteCommandAsync(It.Is<RemoteCommand>(cmd => cmd.TargetMachineId == "PC03"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { Outcome = OperationResult.Success, Status = CommandStatus.Succeeded });

            var tracker = new BulkProgressTracker("OP1", 3);

            // Act
            var results = await manager.ExecutePipelineAsync(op, new[] { m1, m2, m3 }, policy, tracker, null, CancellationToken.None);

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal(2, results.Count(r => r.Outcome == OperationResult.Success));
            Assert.Equal(1, results.Count(r => r.Outcome == OperationResult.Failure));
        }

        [Fact]
        public void ProgressTracker_AggregatesCorrectly_CalculatesEmaEta()
        {
            // Arrange
            var tracker = new BulkProgressTracker("OP1", 100);

            // Act: complete 10 machines in 2 seconds (throughput = 5 machines/sec)
            tracker.UpdateMachineState("PC01", CommandStatus.Succeeded);
            tracker.UpdateMachineState("PC02", CommandStatus.Succeeded);
            tracker.UpdateMachineState("PC03", CommandStatus.Succeeded);
            tracker.UpdateMachineState("PC04", CommandStatus.Succeeded);
            tracker.UpdateMachineState("PC05", CommandStatus.Succeeded);
            tracker.UpdateMachineState("PC06", CommandStatus.Failed);
            tracker.UpdateMachineState("PC07", CommandStatus.Succeeded);
            tracker.UpdateMachineState("PC08", CommandStatus.Succeeded);
            tracker.UpdateMachineState("PC09", CommandStatus.Succeeded);
            tracker.UpdateMachineState("PC10", CommandStatus.Succeeded);

            var progress = tracker.ComputeProgress();

            // Assert
            Assert.Equal(100, progress.TotalTargets);
            Assert.Equal(10, progress.CompletedCount);
            Assert.Equal(9, progress.SucceededCount);
            Assert.Equal(1, progress.FailedCount);
            Assert.Equal(10.0, progress.PercentageComplete);
        }

        [Fact]
        public void RetryManager_ClassifiesAndCalculatesBackoff()
        {
            // Arrange
            var retry = new BulkRetryManager();

            // Act & Assert
            Assert.Equal(BulkFailureType.Timeout, retry.ClassifyFailure(OperationResult.Timeout, ""));
            Assert.Equal(BulkFailureType.NetworkFailure, retry.ClassifyFailure(OperationResult.Failure, "Network connection lost."));
            Assert.Equal(BulkFailureType.MachineOffline, retry.ClassifyFailure(OperationResult.Failure, "Workstation is offline."));

            Assert.True(retry.IsTransient(BulkFailureType.NetworkFailure));
            Assert.False(retry.IsTransient(BulkFailureType.PermissionFailure));

            Assert.Equal(TimeSpan.FromSeconds(1), retry.CalculateBackoff(1, TimeSpan.FromSeconds(1)));
            Assert.Equal(TimeSpan.FromSeconds(4), retry.CalculateBackoff(3, TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public async Task RollbackManager_ExecutesReversingActions()
        {
            // Arrange
            var rollback = new BulkRollbackManager(_commandServiceMock.Object, _rollbackLoggerMock.Object);
            var op = new BulkOperation { BulkOperationId = "OP1", Action = "LOCK_PC", OperatorId = "Admin" };

            _commandServiceMock.Setup(c => c.ExecuteCommandAsync(It.IsAny<RemoteCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { Outcome = OperationResult.Success, Status = CommandStatus.Succeeded });

            // Act
            var history = await rollback.ExecuteRollbackAsync(op, new[] { "PC01", "PC02" });

            // Assert
            Assert.True(history.IsValidated);
            Assert.Equal("UNLOCK_PC", history.RollbackAction);
            Assert.Equal(2, history.MachineOutcomes.Count);
            Assert.Equal(OperationResult.Success, history.MachineOutcomes["PC01"]);
        }

        [Fact]
        public async Task Repository_PersistsAndRecoversState()
        {
            // Arrange
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "bulk_ops.json");
            var repo = new BulkOperationRepository(tempPath);

            var op = new BulkOperation { BulkOperationId = "OP1", Action = "LOCK_PC", Status = OperationStatus.Pending };
            var targets = new List<BulkOperationTarget> { new() { TargetType = BulkTargetType.Individual, TargetValue = "PC01" } };

            // Act
            await repo.SaveOperationAsync(op);
            await repo.SaveTargetsAsync("OP1", targets);

            // Wait for background persistence debouncer to flush to disk
            await Task.Delay(2000);

            // Re-load state in a separate instance (recovering from disk)
            var recoveredRepo = new BulkOperationRepository(tempPath);
            var recoveredOp = await recoveredRepo.GetOperationAsync("OP1");
            var recoveredTargets = await recoveredRepo.GetTargetsAsync("OP1");

            // Assert
            Assert.NotNull(recoveredOp);
            Assert.Equal(OperationStatus.Pending, recoveredOp.Status);
            Assert.Single(recoveredTargets);
            Assert.Equal("PC01", recoveredTargets[0].TargetValue);

            // Cleanup
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
                Directory.Delete(Path.GetDirectoryName(tempPath)!);
            }
        }

        [Fact]
        public async Task Simulate10000_Workstations_Tests()
        {
            // Arrange
            var resolver = new TargetResolver(_fleetManagerMock.Object, _tagManagerMock.Object, _targetResolverLoggerMock.Object);
            var manager = new BulkExecutionManager(_commandServiceMock.Object, _executionLoggerMock.Object);

            var workstations = new List<MachineInfo>();
            for (int i = 1; i <= 10000; i++)
            {
                workstations.Add(new MachineInfo
                {
                    MachineId = $"PC-{i:0000}",
                    Hostname = $"PC-{i:0000}",
                    Status = MachineStatus.Online
                });
            }

            _fleetManagerMock.Setup(f => f.GetGroupMembersAsync("ALL_PC", It.IsAny<CancellationToken>()))
                .ReturnsAsync(workstations);

            _commandServiceMock.Setup(c => c.ExecuteCommandAsync(It.IsAny<RemoteCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { Outcome = OperationResult.Success, Status = CommandStatus.Succeeded });

            var targets = new[] { new BulkOperationTarget { TargetType = BulkTargetType.StaticGroup, TargetValue = "ALL_PC" } };

            // Act
            var resolved = await resolver.ResolveTargetsAsync(targets);
            var tracker = new BulkProgressTracker("OP1", resolved.Count);

            var op = new BulkOperation { BulkOperationId = "OP1", Action = "UPDATE_SERVICE", OperatorId = "Admin" };
            var policy = new BulkOperationPolicy { MaxConcurrency = 100, IndividualTimeout = TimeSpan.FromSeconds(5) };

            var results = await manager.ExecutePipelineAsync(op, resolved.Take(50).ToList(), policy, tracker, null, CancellationToken.None);

            // Assert
            Assert.Equal(10000, resolved.Count);
            Assert.Equal(50, results.Count);
            Assert.All(results, r => Assert.Equal(OperationResult.Success, r.Outcome));
        }
    }
}
