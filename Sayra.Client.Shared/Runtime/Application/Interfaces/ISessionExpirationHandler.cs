using System;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Runtime.Application.Interfaces
{
    public interface ISessionExpirationHandler
    {
        Task HandleExpirationAsync(Guid sessionId);
    }
}
