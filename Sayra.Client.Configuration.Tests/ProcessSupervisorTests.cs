using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Services;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Events;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.States;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Infrastructure.Windows.JobObjects;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Infrastructure.Windows.ProcessMonitoring;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Infrastructure.Windows.ResourceMonitoring;

namespace Sayra.Client.Configuration.Tests
{
    public class ProcessSupervisorTests
    {
        // ==========================================
        // 1. STATE MACHINE TESTS
        // ==========================================
        [Fact]
        public void StateMachine_ValidTransitions_ShouldSucceed()
        {
            // Valid transitions
            Assert.True(ProcessStateMachine.IsValidTransition(ProcessState.Created, ProcessState.Starting));
            Assert.True(ProcessStateMachine.IsValidTransition(ProcessState.Starting, ProcessState.Running));
            Assert.True(ProcessStateMachine.IsValidTransition(ProcessState.Running, ProcessState.Stopping));
            Assert.True(ProcessStateMachine.IsValidTransition(ProcessState.Stopping, ProcessState.Stopped));

            // Unknown fallback transition is always valid
            Assert.True(ProcessStateMachine.IsValidTransition(ProcessState.Running, ProcessState.Unknown));
        }

        [Fact]
        public void StateMachine_InvalidTransitions_ShouldThrowInvalidOperationException()
        {
            // Invalid transition from Stopped to Running
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ProcessStateMachine.ValidateTransition(ProcessState.Stopped, ProcessState.Running));

            Assert.Contains("Invalid process state transition", ex.Message);
        }

        // ==========================================
        // 2. JOB OBJECT MANAGER TESTS
        // ==========================================
        [Fact]
        public void JobObjectManager_CreateAndAssign_ShouldExecuteSuccessfully()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<JobObjectManager>>();
            using var jobManager = new JobObjectManager(loggerMock.Object);
            var runtimeId = Guid.NewGuid();

            // Act & Assert
            // This should not throw even on non-Windows platforms (has safe fallback)
            jobManager.CreateJob(runtimeId);

            // Assigning a dummy/current process id
            int currentPid = Process.GetCurrentProcess().Id;
            jobManager.AssignProcess(runtimeId, currentPid);

            // Configure memory limits and CPU masks
            jobManager.ConfigureLimits(runtimeId, 1024 * 1024 * 100, 1); // 100MB, Core 0

            // Terminate job
            jobManager.TerminateJob(runtimeId);
        }

        // ==========================================
        // 3. PROCESS RESOURCE MONITOR TESTS
        // ==========================================
        [Fact]
        public async Task ProcessResourceMonitor_ReadCurrentProcessMetrics_ShouldSucceed()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ProcessResourceMonitor>>();
            var resourceMonitor = new ProcessResourceMonitor(loggerMock.Object);
            int currentPid = Process.GetCurrentProcess().Id;

            // Act
            var metrics = await resourceMonitor.MonitorMetricsAsync(currentPid);

            // Assert
            Assert.NotNull(metrics);
            Assert.True(metrics.MemoryUsageBytes > 0, "Memory usage should be greater than 0");
            Assert.True(metrics.HandleCount > 0, "Handle count should be greater than 0");
            Assert.True(metrics.CpuUsagePercentage >= 0, "CPU usage should be non-negative");
        }

        [Fact]
        public async Task ProcessResourceMonitor_MissingProcess_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ProcessResourceMonitor>>();
            var resourceMonitor = new ProcessResourceMonitor(loggerMock.Object);
            int nonExistentPid = 999999; // Highly unlikely to exist

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                resourceMonitor.MonitorMetricsAsync(nonExistentPid));
        }

        // ==========================================
        // 4. PROCESS TREE MONITOR TESTS
        // ==========================================
        [Fact]
        public async Task ProcessTreeMonitor_GetDescendants_ShouldExecute()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ProcessTreeMonitor>>();
            var treeMonitor = new ProcessTreeMonitor(loggerMock.Object);
            int currentPid = Process.GetCurrentProcess().Id;

            // Act
            var descendants = await treeMonitor.GetDescendantsAsync(currentPid);

            // Assert
            Assert.NotNull(descendants);
        }

        [Fact]
        public void ProcessTreeMonitor_UnexpectedProcessDetectedEvent_ShouldFire()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ProcessTreeMonitor>>();
            var treeMonitor = new ProcessTreeMonitor(loggerMock.Object);
            var runtimeId = Guid.NewGuid();
            var dummyNode = new ProcessNode
            {
                ProcessId = 1234,
                ProcessName = "cheat_engine.exe"
            };

            bool eventFired = false;
            treeMonitor.UnexpectedProcessDetected += (rid, node) =>
            {
                if (rid == runtimeId && node.ProcessId == 1234)
                {
                    eventFired = true;
                }
            };

            // Act
            treeMonitor.TriggerUnexpectedProcess(runtimeId, dummyNode);

            // Assert
            Assert.True(eventFired);
        }

        // ==========================================
        // 5. PROCESS SUPERVISOR TESTS
        // ==========================================
        [Fact]
        public async Task ProcessSupervisor_RegisterAndStop_ShouldManageLifetimeCorrectly()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ProcessSupervisor>>();
            var eventPublisherMock = new Mock<IRuntimeEventPublisher>();
            var jobManagerMock = new Mock<IJobObjectManager>();
            var treeMonitorMock = new Mock<IProcessTreeMonitor>();
            var resourceMonitorMock = new Mock<IProcessResourceMonitor>();

            using var supervisor = new ProcessSupervisor(
                loggerMock.Object,
                eventPublisherMock.Object,
                jobManagerMock.Object,
                treeMonitorMock.Object,
                resourceMonitorMock.Object
            );

            var runtimeId = Guid.NewGuid();
            var processId = Process.GetCurrentProcess().Id;
            var procInfo = new ProcessInfo
            {
                RuntimeId = runtimeId,
                ProcessId = processId,
                ProcessName = "TestProcess",
                ExecutablePath = "C:\\Test\\TestProcess.exe"
            };

            // Act - Register Process
            await supervisor.RegisterAsync(procInfo);

            // Assert Status is Running after successful registration
            var status = await supervisor.GetStatusAsync(runtimeId);
            Assert.Equal(ProcessState.Running, status.State);
            Assert.Equal(processId, status.ProcessId);

            // Verify Job Object was created and process was assigned
            jobManagerMock.Verify(x => x.CreateJob(runtimeId), Times.Once);
            jobManagerMock.Verify(x => x.AssignProcess(runtimeId, processId), Times.Once);

            // Verify Events Published
            eventPublisherMock.Verify(x => x.Publish(It.IsAny<ProcessRegisteredEvent>()), Times.Once);
            eventPublisherMock.Verify(x => x.Publish(It.IsAny<ProcessStartedEvent>()), Times.Once);

            // Act - Stop Process
            await supervisor.StopAsync(runtimeId);

            // Assert Status is Stopped after successful stop
            status = await supervisor.GetStatusAsync(runtimeId);
            Assert.Equal(ProcessState.Stopped, status.State);

            // Verify Job Object termination was called
            jobManagerMock.Verify(x => x.TerminateJob(runtimeId), Times.Once);
            eventPublisherMock.Verify(x => x.Publish(It.IsAny<ProcessExitedEvent>()), Times.Once);
        }
    }
}
