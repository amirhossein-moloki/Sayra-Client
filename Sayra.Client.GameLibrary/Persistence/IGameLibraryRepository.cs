using System.Collections.Generic;
using System.Threading.Tasks;
using Sayra.Client.GameLibrary.Models;

namespace Sayra.Client.GameLibrary.Persistence
{
    public interface IGameLibraryRepository
    {
        Task<IEnumerable<Game>> GetGamesAsync();
        Task SaveGamesAsync(IEnumerable<Game> games);
        Task<IEnumerable<Application>> GetApplicationsAsync();
        Task SaveApplicationsAsync(IEnumerable<Application> applications);
    }
}
