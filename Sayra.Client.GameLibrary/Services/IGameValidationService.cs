using System.Threading.Tasks;
using Sayra.Client.GameLibrary.Models;

namespace Sayra.Client.GameLibrary.Services
{
    public enum GameValidationStatus
    {
        Installed,
        Missing,
        Corrupted,
        Disabled,
        NeedsVerification,
        Unsupported,
        Unknown
    }

    public class GameValidationResult
    {
        public GameValidationStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsPlayable { get; set; }
    }

    public interface IGameValidationService
    {
        Task<GameValidationResult> ValidateGameAsync(Game game);
    }
}
