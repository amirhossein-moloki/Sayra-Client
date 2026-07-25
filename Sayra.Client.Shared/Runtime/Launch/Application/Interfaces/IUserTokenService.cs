using System;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Runtime.Launch.Application.Interfaces
{
    public interface IUserTokenService
    {
        Task<IntPtr> GetUserTokenAsync(uint sessionId);
        Task<bool> ValidateTokenAsync(IntPtr hToken);
        Task ReleaseTokenAsync(IntPtr hToken);
    }
}
