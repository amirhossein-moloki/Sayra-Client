namespace Sayra.Client.Shared.Runtime.Overlay.Domain.States
{
    /// <summary>
    /// Represents the discrete finite states of the runtime overlay lifecycle.
    /// </summary>
    public enum OverlayState
    {
        Hidden,
        Initializing,
        Visible,
        Updating,
        Closing,
        Disposed
    }
}
