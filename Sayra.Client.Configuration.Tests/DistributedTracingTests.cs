using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Logging;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Exceptions;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Telemetry.Tracing;
using Sayra.Client.Shared.Telemetry.Tracing.Events;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// High-rigor xUnit test suite validating the Phase 8 Stage 4 Enterprise Distributed Tracing Platform.
    /// </summary>
    public class DistributedTracingTests
    {
        private readonly TracingService _tracingService;
        private readonly MockEventDispatcher _eventDispatcher;

        public DistributedTracingTests()
        {
            var options = Options.Create(new TracingOptions
            {
                SamplingProbability = 1.0,
                MaxTraceDepth = 10,
                RequestTimeoutMilliseconds = 5000
            });
            _eventDispatcher = new MockEventDispatcher();
            _tracingService = new TracingService(NullLogger<TracingService>.Instance, options, _eventDispatcher);
        }

        [Fact]
        public async Task StartTraceAsync_CreatesValidTraceContext_WithDefaultValues()
        {
            // Act
            var context = await _tracingService.StartTraceAsync("TestOperation");

            // Assert
            Assert.NotNull(context);
            Assert.NotNull(context.TraceId);
            Assert.NotNull(context.CorrelationId);
            Assert.NotEmpty(context.OperationId);
            Assert.Null(context.ParentOperationId);
            Assert.Equal(Environment.MachineName, context.MachineId);
            Assert.Equal(TraceResult.Success, context.Result);
            Assert.Null(context.Exception);

            // Verify with ambient state
            Assert.Equal(context, _tracingService.CurrentContext);
            Assert.Equal(context.TraceId.Value, TracingContext.TraceId);
            Assert.Equal(context.CorrelationId.Value, TracingContext.CorrelationId);
        }

        [Fact]
        public async Task NestedOperations_InheritTraceAndCorrelationIds_AndSetParentOperationId()
        {
            // Act
            var parent = await _tracingService.StartTraceAsync("ParentOperation");
            var child = await _tracingService.StartTraceAsync("ChildOperation");

            // Assert
            Assert.NotNull(parent);
            Assert.NotNull(child);
            Assert.Equal(parent.TraceId, child.TraceId);
            Assert.Equal(parent.CorrelationId, child.CorrelationId);
            Assert.Equal(parent.OperationId, child.ParentOperationId);
            Assert.NotEqual(parent.OperationId, child.OperationId);
        }

        [Fact]
        public async Task TraceScope_Disposal_RestoresParentAmbientContext()
        {
            // Act
            using (var parentScope = await _tracingService.CreateScopeAsync("Parent"))
            {
                var parentContext = parentScope.Context;
                Assert.Equal(parentContext, _tracingService.CurrentContext);

                using (var childScope = await _tracingService.CreateScopeAsync("Child"))
                {
                    var childContext = childScope.Context;
                    Assert.Equal(childContext, _tracingService.CurrentContext);
                    Assert.Equal(parentContext.OperationId, childContext.ParentOperationId);
                }

                // Child disposed -> parent context should be restored
                Assert.Equal(parentContext, _tracingService.CurrentContext);
            }

            // Parent disposed -> current context should be null
            Assert.Null(_tracingService.CurrentContext);
            Assert.Null(TracingContext.TraceId);
            Assert.Null(TracingContext.CorrelationId);
        }

        [Fact]
        public async Task TraceScope_DisposalAsync_RestoresParentAmbientContext()
        {
            // Act
            await PoulticeAsync();

            // Verify cleanup
            Assert.Null(_tracingService.CurrentContext);
        }

        private async Task PoulticeAsync()
        {
            await using (var parentScope = await _tracingService.CreateScopeAsync("Parent"))
            {
                var parentContext = parentScope.Context;
                Assert.Equal(parentContext, _tracingService.CurrentContext);

                await using (var childScope = await _tracingService.CreateScopeAsync("Child"))
                {
                    var childContext = childScope.Context;
                    Assert.Equal(childContext, _tracingService.CurrentContext);
                }

                Assert.Equal(parentContext, _tracingService.CurrentContext);
            }
        }

        [Fact]
        public async Task StartTraceAsync_ThrowsTracingException_WhenNestingExceedsMaxTraceDepth()
        {
            // Arrange
            var customOptions = Options.Create(new TracingOptions
            {
                SamplingProbability = 1.0,
                MaxTraceDepth = 3
            });
            var service = new TracingService(NullLogger<TracingService>.Instance, customOptions);

            // Act & Assert
            using var scope1 = await service.CreateScopeAsync("Depth1");
            using var scope2 = await service.CreateScopeAsync("Depth2");
            using var scope3 = await service.CreateScopeAsync("Depth3");

            var ex = await Assert.ThrowsAsync<TracingException>(() => service.StartTraceAsync("Depth4"));
            Assert.Contains("limit of 3 spans exceeded", ex.Message);
        }

        [Fact]
        public async Task TraceScope_CaptureException_MarksTraceAsFailedWithDetails()
        {
            // Act
            TraceContext capturedContext;
            using (var scope = await _tracingService.CreateScopeAsync("FailingOperation"))
            {
                try
                {
                    throw new InvalidOperationException("Simulated processing failure");
                }
                catch (Exception ex)
                {
                    scope.CaptureException(ex);
                }
                capturedContext = scope.Context;
            }

            // Assert
            Assert.Equal(TraceResult.Failed, _eventDispatcher.LastResult);
            Assert.Contains("InvalidOperationException: Simulated processing failure", _eventDispatcher.LastException);
        }

        [Fact]
        public async Task AsyncLocalContext_FlowsThroughTaskRunAndAsyncAwaitBoundaries()
        {
            // Act
            using (var scope = await _tracingService.CreateScopeAsync("OuterTask"))
            {
                var outerContext = scope.Context;
                Assert.Equal(outerContext, _tracingService.CurrentContext);

                await Task.Run(async () =>
                {
                    // Verify context propagated to background thread task
                    Assert.Equal(outerContext, _tracingService.CurrentContext);

                    using (var innerScope = await _tracingService.CreateScopeAsync("InnerTask"))
                    {
                        var innerContext = innerScope.Context;
                        Assert.Equal(innerContext, _tracingService.CurrentContext);
                        Assert.Equal(outerContext.TraceId, innerContext.TraceId);
                    }

                    // Inner scope disposed -> restored to outer
                    Assert.Equal(outerContext, _tracingService.CurrentContext);
                });

                // Context on outer task should remain unaffected by child task's completion
                Assert.Equal(outerContext, _tracingService.CurrentContext);
            }
        }

        [Fact]
        public async Task ConcurrentOperations_AreThreadIsolatedAndDoNotCorruptEachOther()
        {
            // Arrange
            int taskCount = 20;
            var completedCount = 0;
            var errors = new ConcurrentBag<string>();

            // Act
            var tasks = new Task[taskCount];
            for (int i = 0; i < taskCount; i++)
            {
                int localIndex = i;
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        var opName = $"ConcurrentOp_{localIndex}";
                        using (var scope = await _tracingService.CreateScopeAsync(opName))
                        {
                            var initialContext = scope.Context;
                            Assert.Equal(initialContext, _tracingService.CurrentContext);

                            // Simulate asynchronous workload and jitter
                            await Task.Delay(new Random().Next(5, 50));

                            // Verify context was preserved across await boundaries
                            if (_tracingService.CurrentContext != initialContext)
                            {
                                errors.Add($"Task {localIndex}: CurrentContext mismatched after Delay!");
                            }

                            using (var innerScope = await _tracingService.CreateScopeAsync($"SubOp_{localIndex}"))
                            {
                                if (innerScope.Context.TraceId != initialContext.TraceId)
                                {
                                    errors.Add($"Task {localIndex}: Inner trace ID did not inherit outer trace ID.");
                                }
                            }

                            if (_tracingService.CurrentContext != initialContext)
                            {
                                errors.Add($"Task {localIndex}: CurrentContext was not restored back after inner scope disposal!");
                            }
                        }

                        Interlocked.Increment(ref completedCount);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Task {localIndex} threw exception: {ex.Message}");
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Empty(errors);
            Assert.Equal(taskCount, completedCount);
        }

        [Fact]
        public async Task EndTraceAsync_DispatchesExpectedTraceCompletedEvent()
        {
            // Arrange
            var context = await _tracingService.StartTraceAsync("SampleEventTrace");

            // Act
            await _tracingService.EndTraceAsync(context, TraceResult.Timeout, "Operation timed out after 5s");

            // Assert
            Assert.NotNull(_eventDispatcher.LastContext);
            Assert.Equal(context.TraceId, _eventDispatcher.LastContext.TraceId);
            Assert.Equal(TraceResult.Timeout, _eventDispatcher.LastResult);
            Assert.Equal("Operation timed out after 5s", _eventDispatcher.LastException);
        }

        [Fact]
        public async Task CreateCorrelationId_ReturnsUniqueValues()
        {
            // Act
            var corrId1 = _tracingService.CreateCorrelationId();
            var corrId2 = _tracingService.CreateCorrelationId();

            // Assert
            Assert.NotNull(corrId1);
            Assert.NotNull(corrId2);
            Assert.NotEqual(corrId1, corrId2);
        }

        private class MockEventDispatcher : IEventDispatcher
        {
            public TraceContext LastContext { get; private set; }
            public TraceResult LastResult { get; private set; }
            public string LastException { get; private set; }

            public void Dispatch<T>(T @event)
            {
                if (@event is TraceCompletedEvent completed)
                {
                    LastContext = completed.Context;
                    LastResult = completed.Result;
                    LastException = completed.Exception;
                }
            }

            public void RegisterHandler<T>(Action<T> handler)
            {
                // Not needed for mock
            }
        }
    }
}
