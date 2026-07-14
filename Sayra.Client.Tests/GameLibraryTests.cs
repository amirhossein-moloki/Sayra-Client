using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Sayra.Client.GameLibrary.Models;
using Sayra.Client.GameLibrary.Persistence;
using Sayra.Client.GameLibrary.Services;

namespace Sayra.Client.Tests
{
    public class GameLibraryTests : IDisposable
    {
        private readonly string _testDataDir;

        public GameLibraryTests()
        {
            // Use a unique folder for each test execution to prevent interference
            _testDataDir = Path.Combine(Path.GetTempPath(), "SayraTestGameLibrary_" + Guid.NewGuid().ToString());
            if (Directory.Exists(_testDataDir))
            {
                Directory.Delete(_testDataDir, true);
            }
            Directory.CreateDirectory(_testDataDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDataDir))
            {
                try
                {
                    Directory.Delete(_testDataDir, true);
                }
                catch
                {
                    // Ignore cleanup errors in Dispose
                }
            }
        }

        [Fact]
        public async Task AddGame_WithValidPath_ShouldAddSuccessfully()
        {
            // Arrange
            var repo = new GameLibraryRepository(_testDataDir, null);
            var service = new GameLibraryService(repo, null);

            // Create a temporary file to act as a valid executable
            string dummyExe = Path.Combine(_testDataDir, "valid_game.exe");
            await File.WriteAllTextAsync(dummyExe, "MZ..."); // Dummy exe headers

            var game = new Game
            {
                Name = "My Test Game",
                ExecutablePath = dummyExe,
                Arguments = "-windowed",
                WorkingDirectory = _testDataDir,
                IconPath = "icon.png",
                Enabled = true,
                Source = GameSource.Manual,
                Category = new GameCategory { Id = "action", Name = "Action" }
            };

            // Act
            await service.AddGame(game);

            // Assert
            var games = (await service.GetGames()).ToList();
            Assert.Single(games);

            var savedGame = games.First();
            Assert.False(string.IsNullOrWhiteSpace(savedGame.Id));
            Assert.Equal("My Test Game", savedGame.Name);
            Assert.Equal(dummyExe, savedGame.ExecutablePath);
            Assert.Equal("-windowed", savedGame.Arguments);
            Assert.Equal(_testDataDir, savedGame.WorkingDirectory);
            Assert.Equal("icon.png", savedGame.IconPath);
            Assert.True(savedGame.Enabled);
            Assert.Equal(GameSource.Manual, savedGame.Source);
            Assert.Equal("action", savedGame.Category.Id);
            Assert.Equal("Action", savedGame.Category.Name);
            Assert.True(savedGame.CreatedAt <= DateTime.UtcNow);
            Assert.True(savedGame.UpdatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public async Task AddGame_WithInvalidPath_ShouldThrowArgumentException()
        {
            // Arrange
            var repo = new GameLibraryRepository(_testDataDir, null);
            var service = new GameLibraryService(repo, null);

            string nonExistentPath = Path.Combine(_testDataDir, "does_not_exist.exe");

            var game = new Game
            {
                Name = "Invalid Path Game",
                ExecutablePath = nonExistentPath,
                Category = new GameCategory { Id = "rpg", Name = "RPG" }
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.AddGame(game));
            Assert.Contains("Invalid executable path", exception.Message);

            var games = await service.GetGames();
            Assert.Empty(games);
        }

        [Fact]
        public async Task RemoveGame_ShouldRemoveSuccessfully()
        {
            // Arrange
            var repo = new GameLibraryRepository(_testDataDir, null);
            var service = new GameLibraryService(repo, null);

            string dummyExe = Path.Combine(_testDataDir, "valid_game_remove.exe");
            await File.WriteAllTextAsync(dummyExe, "MZ...");

            var game = new Game
            {
                Id = "game-to-remove",
                Name = "Removable Game",
                ExecutablePath = dummyExe,
                Category = new GameCategory { Id = "sports", Name = "Sports" }
            };

            await service.AddGame(game);

            var gamesBefore = (await service.GetGames()).ToList();
            Assert.Single(gamesBefore);

            // Act
            await service.RemoveGame("game-to-remove");

            // Assert
            var gamesAfter = await service.GetGames();
            Assert.Empty(gamesAfter);
        }

        [Fact]
        public async Task CorruptedJsonRecovery_ShouldRecoverFromBackup()
        {
            // Arrange
            var repo = new GameLibraryRepository(_testDataDir, null);

            var games = new List<Game>
            {
                new Game
                {
                    Id = "g1",
                    Name = "Saved Game",
                    ExecutablePath = "C:\\game.exe",
                    Category = new GameCategory { Id = "rts", Name = "RTS" }
                }
            };

            // Save list -> This creates games.json
            await repo.SaveGamesAsync(games);
            // Save list again -> This triggers backup creation because games.json already exists
            await repo.SaveGamesAsync(games);

            string mainFilePath = Path.Combine(_testDataDir, "games.json");
            string backupFilePath = Path.Combine(_testDataDir, "games.json.bak");

            Assert.True(File.Exists(mainFilePath), "Main file should exist");
            Assert.True(File.Exists(backupFilePath), "Backup file should exist");

            // Corrupt the main file with invalid JSON
            await File.WriteAllTextAsync(mainFilePath, "{ corrupted json: ");

            // Create a new repository instance to simulate a fresh startup/read
            var recoveryRepo = new GameLibraryRepository(_testDataDir, null);

            // Act
            var recoveredGames = (await recoveryRepo.GetGamesAsync()).ToList();

            // Assert
            Assert.Single(recoveredGames);
            Assert.Equal("g1", recoveredGames.First().Id);
            Assert.Equal("Saved Game", recoveredGames.First().Name);

            // Verify that the main file is restored to a valid state
            string mainFileContent = await File.ReadAllTextAsync(mainFilePath);
            Assert.Contains("Saved Game", mainFileContent);
        }

        [Fact]
        public async Task PersistenceAfterRestart_ShouldLoadSavedData()
        {
            // Arrange
            var repo1 = new GameLibraryRepository(_testDataDir, null);

            var games = new List<Game>
            {
                new Game
                {
                    Id = "persisted-1",
                    Name = "GTA VI",
                    ExecutablePath = "C:\\gtavi.exe",
                    Category = new GameCategory { Id = "action", Name = "Action" }
                }
            };

            // Save games using first repository instance
            await repo1.SaveGamesAsync(games);

            // Simulate application restart by instantiating a completely new repo instance pointing to the same data directory
            var repo2 = new GameLibraryRepository(_testDataDir, null);

            // Act
            var loadedGames = (await repo2.GetGamesAsync()).ToList();

            // Assert
            Assert.Single(loadedGames);
            Assert.Equal("persisted-1", loadedGames.First().Id);
            Assert.Equal("GTA VI", loadedGames.First().Name);
        }
    }
}
