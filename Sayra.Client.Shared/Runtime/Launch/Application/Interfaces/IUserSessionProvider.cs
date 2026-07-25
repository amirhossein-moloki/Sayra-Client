using System.Threading.Tasks;

namespace Sayra.Client.Shared.Runtime.Launch.Application.Interfaces
{
    public class UserSessionInfo
    {
        public uint SessionId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string UserSid { get; set; } = string.Empty;
        public bool IsInteractive { get; set; }
    }

    public interface IUserSessionProvider
    {
        Task<UserSessionInfo> GetActiveSessionAsync();
    }
}
