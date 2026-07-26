using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Overlay.Domain.Models;

namespace Sayra.Client.Shared.Runtime.Overlay.Application.Services
{
    public class WpfOverlayRenderer : IOverlayRenderer
    {
        private readonly ILogger<WpfOverlayRenderer> _logger;
        private readonly IOverlayWindowService _windowService;

        public bool IsSupported => true; // WPF is always supported on active Windows interactive sessions

        public WpfOverlayRenderer(ILogger<WpfOverlayRenderer> logger, IOverlayWindowService windowService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        }

        public async Task RenderAsync(OverlayData data)
        {
            _logger.LogInformation("WpfOverlayRenderer: Showing and updating WPF overlay window.");
            await _windowService.ShowWindowAsync();
            await _windowService.UpdateContentAsync(data);
        }

        public async Task ClearAsync()
        {
            _logger.LogInformation("WpfOverlayRenderer: Hiding/closing WPF overlay window.");
            await _windowService.HideWindowAsync();
        }
    }
}
