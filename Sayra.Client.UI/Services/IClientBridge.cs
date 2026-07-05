using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sayra.Client.UI.Services
{
    public enum ClientStatus
    {
        Disconnected,
        Connecting,
        Authenticating,
        Connected,
        Syncing
    }

    public enum SessionState
    {
        Idle,
        Active,
        Paused,
        Ended
    }

    public class ClientState
    {
        public ClientStatus Status { get; set; }
        public SessionState SessionState { get; set; }
        public TimeSpan RemainingTime { get; set; }
        public string? UserName { get; set; }
    }

    public class AppModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
    }

    public interface IClientBridge
    {
        Task<ClientState> GetState();
        Task SendCommand(string action, object? parameters = null);
        IObservable<ClientState> SubscribeToStateChanged();
        Task<IEnumerable<AppModel>> GetApplications();
    }
}
