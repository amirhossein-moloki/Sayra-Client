using System.Threading.Tasks;
using Sayra.Client.Shared.Runtime.Overlay.Domain.Models;

namespace Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces
{
    public interface IOverlayRenderer
    {
        /// <summary>
        /// Renders active overlay session visual data.
        /// </summary>
        Task RenderAsync(OverlayData data);

        /// <summary>
        /// Clears or hides the active overlay.
        /// </summary>
        Task ClearAsync();

        /// <summary>
        /// Gets whether this renderer is supported in the current operating system and environment.
        /// </summary>
        bool IsSupported { get; }
    }
}
