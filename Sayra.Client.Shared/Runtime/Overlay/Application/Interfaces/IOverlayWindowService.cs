using System.Threading.Tasks;
using Sayra.Client.Shared.Runtime.Overlay.Domain.Models;

namespace Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces
{
    /// <summary>
    /// Decoupled platform-specific contract to manage physical window rendering and positions.
    /// </summary>
    public interface IOverlayWindowService
    {
        Task ShowWindowAsync();
        Task HideWindowAsync();
        Task UpdateContentAsync(OverlayData data);
    }
}
