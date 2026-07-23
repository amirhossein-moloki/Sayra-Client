using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Services
{
    public class SessionContextProvider : ISessionContextProvider
    {
        private readonly SessionManager _sessionManager;

        public SessionContextProvider(SessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public string? CurrentSessionId => _sessionManager.GetCurrentSession()?.SessionId;
    }
}
