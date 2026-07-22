using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SayraClient;
using SayraClient.Services;
using Xunit;

namespace Sayra.Client.Tests;

public class InfrastructureTests
{
    private readonly Mock<ILogger<ServiceHealthMonitor>> _healthLoggerMock = new();
    private readonly Mock<ILogger<WorkerSupervisor>> _supervisorLoggerMock = new();
    private readonly Mock<ILogger<HeartbeatManager>> _heartbeatLoggerMock = new();
    private readonly Mock<ILogger<ModuleLifecycleManager>> _moduleLoggerMock = new();

    [Fact]
    public void ServiceHealthMonitor_Should_TrackStates_And_CalculateOverallHealth()
    {
        // Arrange
        var monitor = new ServiceHealthMonitor(_healthLoggerMock.Object);

        // Act & Assert - Initial health with no registered services should be Healthy
        Assert.Equal(ServiceHealthState.Healthy, monitor.GetOverallHealth());

        // Report one starting service
        monitor.ReportState("ServiceA", ServiceHealthState.Starting, "Initiating");
        Assert.Equal(ServiceHealthState.Starting, monitor.GetOverallHealth());

        // Report one failed service
        monitor.ReportState("ServiceB", ServiceHealthState.Failed, "Crashed");
        Assert.Equal(ServiceHealthState.Failed, monitor.GetOverallHealth());

        // Report recovery
        monitor.ReportState("ServiceB", ServiceHealthState.Healthy, "Recovered");
        Assert.Equal(ServiceHealthState.Starting, monitor.GetOverallHealth()); // Still starting ServiceA

        // Complete ServiceA starting
        monitor.ReportState("ServiceA", ServiceHealthState.Healthy, "Running");
        Assert.Equal(ServiceHealthState.Healthy, monitor.GetOverallHealth());
    }

    [Fact]
    public async Task WorkerSupervisor_Should_Respect_Dependency_Sorting_On_Start()
    {
        // Arrange
        var monitor = new ServiceHealthMonitor(_healthLoggerMock.Object);
        var supervisor = new WorkerSupervisor(_supervisorLoggerMock.Object, monitor);
        var orderOfExecution = new List<string>();

        supervisor.RegisterWorker("WorkerA", async (token) =>
        {
            await Task.Delay(20, token);
            orderOfExecution.Add("WorkerA");
        }, new[] { "WorkerB" }); // WorkerA depends on WorkerB

        supervisor.RegisterWorker("WorkerB", async (token) =>
        {
            orderOfExecution.Add("WorkerB");
            await Task.CompletedTask;
        });

        // Act
        await supervisor.StartAllAsync(CancellationToken.None);

        // Allow some time for background tasks to process
        await Task.Delay(100);

        // Assert
        Assert.Equal(2, orderOfExecution.Count);
        Assert.Equal("WorkerB", orderOfExecution[0]); // WorkerB must start first
        Assert.Equal("WorkerA", orderOfExecution[1]);
    }

    [Fact]
    public void WorkerSupervisor_Should_Detect_Circular_Dependencies()
    {
        // Arrange
        var monitor = new ServiceHealthMonitor(_healthLoggerMock.Object);
        var supervisor = new WorkerSupervisor(_supervisorLoggerMock.Object, monitor);

        supervisor.RegisterWorker("WorkerA", token => Task.CompletedTask, new[] { "WorkerB" });
        supervisor.RegisterWorker("WorkerB", token => Task.CompletedTask, new[] { "WorkerA" });

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.StartAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ModuleLifecycleManager_Should_Run_Lifecycle_In_Order()
    {
        // Arrange
        var manager = new ModuleLifecycleManager(_moduleLoggerMock.Object);
        var mockModule = new Mock<IModule>();
        mockModule.Setup(m => m.Name).Returns("TestModule");
        mockModule.Setup(m => m.Dependencies).Returns(Array.Empty<string>());

        var steps = new List<string>();
        mockModule.Setup(m => m.InitializeAsync(It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("Initialize"))
            .Returns(Task.CompletedTask);
        mockModule.Setup(m => m.StartAsync(It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("Start"))
            .Returns(Task.CompletedTask);
        mockModule.Setup(m => m.StopAsync(It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("Stop"))
            .Returns(Task.CompletedTask);

        // Act
        manager.RegisterModule(mockModule.Object);
        await manager.InitializeAllAsync(CancellationToken.None);
        await manager.StartAllAsync(CancellationToken.None);
        await manager.StopAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(3, steps.Count);
        Assert.Equal("Initialize", steps[0]);
        Assert.Equal("Start", steps[1]);
        Assert.Equal("Stop", steps[2]);
    }
}
