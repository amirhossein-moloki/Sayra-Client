using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Domain.States;
using Sayra.Client.Shared.Runtime.Domain.Events;
using Sayra.Client.Shared.Runtime.Domain.Exceptions;
using Sayra.Client.Shared.Runtime.Application.Interfaces;

namespace Sayra.Client.Shared.Runtime.Application.Services
{
    public class RuntimeStateManager : IRuntimeStateManager
    {
        private readonly ILogger<RuntimeStateManager> _logger;
        private readonly IRuntimeEventPublisher _eventPublisher;
        private readonly object _lock = new();

        public RuntimeState CurrentState { get; private set; } = RuntimeState.Created;

        private static readonly Dictionary<RuntimeState, HashSet<RuntimeState>> AllowedTransitions = new()
        {
            { RuntimeState.Created, new HashSet<RuntimeState> { RuntimeState.Preparing, RuntimeState.Failed } },
            { RuntimeState.Preparing, new HashSet<RuntimeState> { RuntimeState.Starting, RuntimeState.Failed } },
            { RuntimeState.Starting, new HashSet<RuntimeState> { RuntimeState.Running, RuntimeState.Failed } },
            { RuntimeState.Running, new HashSet<RuntimeState> { RuntimeState.Paused, RuntimeState.Stopping, RuntimeState.Warning, RuntimeState.Expired, RuntimeState.Failed } },
            { RuntimeState.Paused, new HashSet<RuntimeState> { RuntimeState.Running, RuntimeState.Stopping, RuntimeState.Failed } },
            { RuntimeState.Warning, new HashSet<RuntimeState> { RuntimeState.Running, RuntimeState.Expired, RuntimeState.Stopping, RuntimeState.Failed } },
            { RuntimeState.Expired, new HashSet<RuntimeState> { RuntimeState.Stopping, RuntimeState.Failed } },
            { RuntimeState.Stopping, new HashSet<RuntimeState> { RuntimeState.Completed, RuntimeState.Failed } },
            { RuntimeState.Completed, new HashSet<RuntimeState>() },
            { RuntimeState.Failed, new HashSet<RuntimeState>() }
        };

        public RuntimeStateManager(ILogger<RuntimeStateManager> logger, IRuntimeEventPublisher eventPublisher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        }

        public void TransitionTo(RuntimeState newState, string? reason = null)
        {
            lock (_lock)
            {
                var oldState = CurrentState;
                if (oldState == newState) return;

                if (!AllowedTransitions.ContainsKey(oldState) || !AllowedTransitions[oldState].Contains(newState))
                {
                    _logger.LogWarning("Invalid state transition attempted: {OldState} -> {NewState}. Reason: {Reason}", oldState, newState, reason);
                    throw new RuntimeTransitionException($"Invalid state transition: {oldState} to {newState}");
                }

                CurrentState = newState;
                _logger.LogInformation("Runtime state changed: {OldState} -> {NewState}. Reason: {Reason}", oldState, newState, reason ?? "None");

                // Publish RuntimeStateChangedEvent
                _eventPublisher.Publish(new RuntimeStateChangedEvent(oldState, newState, reason));

                // Publish specialized events
                if (newState == RuntimeState.Running)
                {
                    _logger.LogInformation("Runtime execution started successfully.");
                    _eventPublisher.Publish(new RuntimeStartedEvent(reason));
                }
                else if (newState == RuntimeState.Completed)
                {
                    _logger.LogInformation("Runtime session completed.");
                    _eventPublisher.Publish(new RuntimeStoppedEvent(reason));
                }
                else if (newState == RuntimeState.Failed)
                {
                    _logger.LogError("Runtime system encountered a critical failure. Reason: {Reason}", reason);
                    _eventPublisher.Publish(new RuntimeFailedEvent(reason));
                }
            }
        }
    }
}
