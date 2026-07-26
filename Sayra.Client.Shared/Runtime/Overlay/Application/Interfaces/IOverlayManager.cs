using System;
using System.Threading.Tasks;
using Sayra.Client.Shared.Runtime.Overlay.Domain.Models;

namespace Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces
{
    /// <summary>
    /// Contract defining the central orchestrator of the overlay lifecycle and visibility.
    /// </summary>
    public interface IOverlayManager : IDisposable
    {
        Task ShowAsync();
        Task HideAsync();
        Task UpdateAsync(OverlayData data);
    }
}
