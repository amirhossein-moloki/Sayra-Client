using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.GameLibrary.Models;
using Sayra.Client.GameLibrary.Persistence;

namespace Sayra.Client.GameLibrary.Services
{
    public class GameLibraryService : IGameLibraryService
    {
        private readonly IGameLibraryRepository _repository;
        private readonly ILogger<GameLibraryService>? _logger;

        public GameLibraryService(IGameLibraryRepository repository, ILogger<GameLibraryService>? logger = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger;
        }

        public async Task<IEnumerable<Game>> GetGames()
        {
            return await _repository.GetGamesAsync();
        }

        public async Task AddGame(Game game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            if (!await ValidateGamePath(game.ExecutablePath))
            {
                throw new ArgumentException("Invalid executable path", nameof(game.ExecutablePath));
            }

            if (string.IsNullOrWhiteSpace(game.Id))
            {
                game.Id = Guid.NewGuid().ToString();
            }

            var games = (await _repository.GetGamesAsync()).ToList();

            if (games.Any(g => g.Id == game.Id))
            {
                throw new ArgumentException($"Game with ID {game.Id} already exists.");
            }

            game.CreatedAt = DateTime.UtcNow;
            game.UpdatedAt = DateTime.UtcNow;

            games.Add(game);
            await _repository.SaveGamesAsync(games);
        }

        public async Task UpdateGame(Game game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            if (!await ValidateGamePath(game.ExecutablePath))
            {
                throw new ArgumentException("Invalid executable path", nameof(game.ExecutablePath));
            }

            var games = (await _repository.GetGamesAsync()).ToList();
            var index = games.FindIndex(g => g.Id == game.Id);
            if (index < 0)
            {
                throw new KeyNotFoundException($"Game with ID {game.Id} was not found.");
            }

            game.UpdatedAt = DateTime.UtcNow;
            // Preserve original CreatedAt
            game.CreatedAt = games[index].CreatedAt;

            games[index] = game;
            await _repository.SaveGamesAsync(games);
        }

        public async Task RemoveGame(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("ID cannot be null or whitespace.", nameof(id));
            }

            var games = (await _repository.GetGamesAsync()).ToList();
            var existing = games.FirstOrDefault(g => g.Id == id);
            if (existing == null)
            {
                throw new KeyNotFoundException($"Game with ID {id} was not found.");
            }

            games.Remove(existing);
            await _repository.SaveGamesAsync(games);
        }

        public Task<bool> ValidateGamePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Task.FromResult(false);
            }

            try
            {
                bool exists = File.Exists(path);
                return Task.FromResult(exists);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error validating path {Path}", path);
                return Task.FromResult(false);
            }
        }
    }
}
