using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Overlay.Domain.Models;

namespace Sayra.UI.Overlay.Infrastructure.Windows.OverlayWindow
{
    /// <summary>
    /// Implements IOverlayWindowService on top of standard WPF elements safely invoking on UI dispatcher thread.
    /// </summary>
    public class OverlayWindowService : IOverlayWindowService
    {
        private readonly ILogger<OverlayWindowService> _logger;
        private OverlayWindow? _overlayWindow;

        public OverlayWindowService(ILogger<OverlayWindowService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task ShowWindowAsync()
        {
            return ExecuteOnUIThreadAsync(() =>
            {
                if (_overlayWindow == null)
                {
                    _logger.LogInformation("OverlayWindowService: Constructing new OverlayWindow view.");
                    _overlayWindow = new OverlayWindow();
                }

                _logger.LogInformation("OverlayWindowService: Displaying overlay window.");
                _overlayWindow.Show();
                _overlayWindow.Topmost = true;
            });
        }

        public Task HideWindowAsync()
        {
            return ExecuteOnUIThreadAsync(() =>
            {
                if (_overlayWindow != null)
                {
                    _logger.LogInformation("OverlayWindowService: Hiding and closing overlay window.");
                    _overlayWindow.Hide();
                    _overlayWindow.Close();
                    _overlayWindow = null;
                }
            });
        }

        public Task UpdateContentAsync(OverlayData data)
        {
            return ExecuteOnUIThreadAsync(() =>
            {
                if (_overlayWindow == null)
                {
                    _logger.LogWarning("OverlayWindowService: Window is not currently visible, auto-initializing instance.");
                    _overlayWindow = new OverlayWindow();
                    _overlayWindow.Show();
                    _overlayWindow.Topmost = true;
                }

                _overlayWindow.UpdateData(data);
            });
        }

        private Task ExecuteOnUIThreadAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher == null)
            {
                _logger.LogWarning("OverlayWindowService: WPF Application Dispatcher is not available. Performing action synchronously.");
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OverlayWindowService: Failed during synchronous fallback processing.");
                    tcs.SetException(ex);
                }
                return tcs.Task;
            }

            if (dispatcher.CheckAccess())
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }
            else
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        action();
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));
            }

            return tcs.Task;
        }
    }
}
