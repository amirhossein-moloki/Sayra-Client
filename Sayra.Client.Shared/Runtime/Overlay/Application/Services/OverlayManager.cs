using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Overlay.Domain.Models;
using Sayra.Client.Shared.Runtime.Overlay.Domain.States;

namespace Sayra.Client.Shared.Runtime.Overlay.Application.Services
{
    /// <summary>
    /// Implements IOverlayManager to coordinate overlay show, hide, and update states and window service tasks.
    /// </summary>
    public class OverlayManager : IOverlayManager
    {
        private readonly ILogger<OverlayManager> _logger;
        private readonly IOverlayDataProvider _dataProvider;
        private readonly IOverlayWindowService _windowService;
        private readonly OverlayStateMachine _stateMachine;
        private readonly object _lock = new();

        public OverlayStateMachine StateMachine => _stateMachine;

        public OverlayManager(
            ILogger<OverlayManager> logger,
            IOverlayDataProvider dataProvider,
            IOverlayWindowService windowService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _stateMachine = new OverlayStateMachine(logger);

            _stateMachine.TransitionTo(OverlayState.Initializing);
            _dataProvider.DataUpdated += OnDataProviderUpdated;
            _stateMachine.TransitionTo(OverlayState.Hidden);
        }

        private void OnDataProviderUpdated(OverlayData data)
        {
            lock (_lock)
            {
                if (_stateMachine.CurrentState == OverlayState.Disposed) return;

                _logger.LogInformation("OverlayManager: Received data update notification. State={State}, Visibility={Visibility}", data.SessionState, data.Visibility);

                // Process asynchronously to avoid deadlocks or thread starvation on event publisher thread
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (data.Visibility)
                        {
                            await ShowAsync();
                            await UpdateAsync(data);
                        }
                        else
                        {
                            await HideAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "OverlayManager: Error occurred processing data update asynchronously.");
                    }
                });
            }
        }

        public async Task ShowAsync()
        {
            lock (_lock)
            {
                if (_stateMachine.CurrentState == OverlayState.Disposed) return;
                if (_stateMachine.CurrentState == OverlayState.Visible || _stateMachine.CurrentState == OverlayState.Updating) return;

                _stateMachine.TransitionTo(OverlayState.Visible);
            }

            _logger.LogInformation("OverlayManager: Invoking window service to show overlay...");
            await _windowService.ShowWindowAsync();
        }

        public async Task HideAsync()
        {
            lock (_lock)
            {
                if (_stateMachine.CurrentState == OverlayState.Disposed) return;
                if (_stateMachine.CurrentState == OverlayState.Hidden || _stateMachine.CurrentState == OverlayState.Closing) return;

                _stateMachine.TransitionTo(OverlayState.Closing);
            }

            _logger.LogInformation("OverlayManager: Invoking window service to hide/close overlay...");
            await _windowService.HideWindowAsync();

            lock (_lock)
            {
                if (_stateMachine.CurrentState == OverlayState.Closing)
                {
                    _stateMachine.TransitionTo(OverlayState.Hidden);
                }
            }
        }

        public async Task UpdateAsync(OverlayData data)
        {
            lock (_lock)
            {
                if (_stateMachine.CurrentState == OverlayState.Disposed) return;
                if (_stateMachine.CurrentState != OverlayState.Visible && _stateMachine.CurrentState != OverlayState.Updating) return;

                _stateMachine.TransitionTo(OverlayState.Updating);
            }

            _logger.LogInformation("OverlayManager: Updating window content with new remaining duration: {Remaining}", data.RemainingTime);
            await _windowService.UpdateContentAsync(data);

            lock (_lock)
            {
                if (_stateMachine.CurrentState == OverlayState.Updating)
                {
                    _stateMachine.TransitionTo(OverlayState.Visible);
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_stateMachine.CurrentState == OverlayState.Disposed) return;

                _dataProvider.DataUpdated -= OnDataProviderUpdated;
                _stateMachine.TransitionTo(OverlayState.Disposed);
                _logger.LogInformation("OverlayManager: Successfully disposed and unsubscribed from data provider.");
            }
        }
    }
}
