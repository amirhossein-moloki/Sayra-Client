using System;

namespace Sayra.Client.LocalAdmin.Authentication
{
    public interface IAdminSessionManager
    {
        string CreateSession(string username, TimeSpan? timeout = null);
        bool ValidateSession(string token);
        void RevokeSession(string token);
        void CleanExpiredSessions();
    }
}
