using System.Collections.Generic;
using System.Threading.Tasks;
using Sayra.Client.GameLibrary.Models;

namespace Sayra.Client.GameLibrary.Services
{
    public interface IGameLibraryService
    {
        Task<IEnumerable<Game>> GetGames();
        Task AddGame(Game game);
        Task UpdateGame(Game game);
        Task RemoveGame(string id);
        Task<bool> ValidateGamePath(string path);
    }
}
