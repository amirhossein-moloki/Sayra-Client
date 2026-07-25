using System.Threading.Tasks;
using Sayra.Client.Shared.Runtime.Launch.Domain.Models;

namespace Sayra.Client.Shared.Runtime.Launch.Application.Interfaces
{
    public interface ILaunchProfileProvider
    {
        Task<LaunchProfile> GetProfileAsync(string gameId);
    }
}
