using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Overlay.Domain.Models;

namespace Sayra.Client.Shared.Runtime.Overlay.Application.Services
{
    public class DxgiOverlayRenderer : IOverlayRenderer
    {
        private readonly ILogger<DxgiOverlayRenderer> _logger;

        // Currently kept as false due to DirectX injection stability and anti-cheat mitigation constraints.
        public bool IsSupported => false;

        public DxgiOverlayRenderer(ILogger<DxgiOverlayRenderer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task RenderAsync(OverlayData data)
        {
            _logger.LogWarning("DxgiOverlayRenderer: Direct3D/DXGI injection rendering is currently not enabled/supported on this platform.");
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            return Task.CompletedTask;
        }
    }
}
