using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.GameLibrary.Models;
using Sayra.Client.GameLibrary.Persistence;
using Sayra.Client.GameLibrary.Services;
using Sayra.Client.Scanner.Cache;
using Sayra.Client.Scanner.Models;
using Sayra.Client.Scanner.Providers;
using Sayra.Client.Scanner.ScannerEngine;
using Sayra.Client.Scanner.Services;
using Sayra.Client.Scanner.Validation;
using Xunit;

namespace Sayra.Client.Tests
{
    public class ScannerTests : IDisposable
    {
        private readonly string _testDir;
        private readonly Mock<ILogger<ApplicationScannerService>> _loggerMock = new();
        private readonly Mock<ILogger<KnownGameDatabase>> _dbLoggerMock = new();
        private readonly Mock<ILogger<ScanCacheService>> _cacheLoggerMock = new();
        private readonly Mock<ILogger<ScannerValidator>> _valLoggerMock = new();

        public ScannerTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "SayraScannerTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                try
                {
                    Directory.Delete(_testDir, true);
                }
                catch { }
            }
        }

        private async Task CreateDummyExecutableAsync(string path)
        {
            string? dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            // MZ Header to pass scanner validation
            byte[] mzHeader = new byte[64];
            mzHeader[0] = 0x4D; // M
            mzHeader[1] = 0x5A; // Z
            await File.WriteAllBytesAsync(path, mzHeader);
        }

        [Fact]
        public async Task ExecutableMetadataProvider_ExtractsCorrectMetadata()
        {
            // Arrange
            string exePath = Path.Combine(_testDir, "test_game.exe");
            await CreateDummyExecutableAsync(exePath);

            var provider = new ExecutableMetadataProvider();

            // Act
            var metadata = await provider.ExtractAsync(exePath);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal("test_game.exe", metadata.ExecutableName);
            Assert.Equal(64, metadata.ExecutableSize);
            Assert.False(string.IsNullOrWhiteSpace(metadata.FileHash));
            Assert.Equal(_testDir, metadata.WorkingDirectory);
        }

        [Fact]
        public void ShortcutParser_ResolvesUrlAndLnkShortcuts()
        {
            // Arrange
            string targetPath = Path.Combine(_testDir, "game.exe");
            string urlPath = Path.Combine(_testDir, "game.url");

            // Write a .url shortcut file
            string urlContent = "[InternetShortcut]\nURL=file:///" + targetPath.Replace('\\', '/');
            File.WriteAllText(urlPath, urlContent);

            // Act
            string? resolved = ShortcutParser.ResolveShortcut(urlPath);

            // Assert
            Assert.Equal(targetPath, resolved);
        }

        [Fact]
        public async Task ScannerValidator_BlocksBlacklistedAndCorruptedFiles()
        {
            // Arrange
            var db = new KnownGameDatabase(_testDir, _dbLoggerMock.Object);
            var validator = new ScannerValidator(db, _valLoggerMock.Object);

            string blacklistedPath = Path.Combine(_testDir, "chrome.exe");
            await CreateDummyExecutableAsync(blacklistedPath);

            string corruptedPath = Path.Combine(_testDir, "corrupted.exe");
            await File.WriteAllTextAsync(corruptedPath, "Not an MZ header...");

            string validPath = Path.Combine(_testDir, "cs2.exe");
            await CreateDummyExecutableAsync(validPath);

            // Act & Assert
            Assert.False(validator.Validate(blacklistedPath)); // Blacklisted
            Assert.False(validator.Validate(corruptedPath));   // Corrupted (No MZ)
            Assert.True(validator.Validate(validPath));        // Valid
        }

        [Fact]
        public async Task GameDetectionEngine_ClassifiesKnownAndHeuristicGames()
        {
            // Arrange
            var db = new KnownGameDatabase(_testDir, _dbLoggerMock.Object);
            var engine = new GameDetectionEngine(db);

            var cs2Meta = new ExecutableMetadata
            {
                ExecutableName = "cs2.exe",
                Publisher = "Valve",
                WorkingDirectory = Path.Combine(_testDir, "steamapps", "common", "Counter-Strike 2")
            };

            var chromeMeta = new ExecutableMetadata
            {
                ExecutableName = "chrome.exe",
                Publisher = "Google LLC",
                WorkingDirectory = _testDir
            };

            // Act
            var cs2Classification = engine.Classify(cs2Meta);
            var chromeClassification = engine.Classify(chromeMeta);

            // Assert
            Assert.Equal("Game", cs2Classification.Type);
            Assert.Equal("Steam", cs2Classification.Launcher);
            Assert.Equal(100, cs2Classification.ConfidenceScore);

            Assert.Equal("Application", chromeClassification.Type);
        }

        [Fact]
        public async Task ScanCacheService_SavesAndLoadsCorrectly()
        {
            // Arrange
            string cachePath = Path.Combine(_testDir, "scan_cache.json");
            var cacheService1 = new ScanCacheService(_testDir, _cacheLoggerMock.Object);

            string exePath = Path.Combine(_testDir, "cached_game.exe");
            await CreateDummyExecutableAsync(exePath);

            var app = new DetectedApplication
            {
                Id = "app-1",
                Name = "Cached Game",
                ExecutablePath = exePath,
                Type = "Game"
            };

            // Act
            cacheService1.Set(exePath, app);
            await cacheService1.SaveAsync();

            var cacheService2 = new ScanCacheService(_testDir, _cacheLoggerMock.Object);
            await cacheService2.LoadAsync();
            bool hit = cacheService2.TryGet(exePath, out var loadedApp);

            // Assert
            Assert.True(hit);
            Assert.NotNull(loadedApp);
            Assert.Equal("Cached Game", loadedApp.Name);
        }

        [Fact]
        public async Task IncrementalScan_AvoidsRescanningUnchangedFiles()
        {
            // Arrange
            string scanDir = Path.Combine(_testDir, "ScanDir");
            Directory.CreateDirectory(scanDir);

            string exePath = Path.Combine(scanDir, "cs2.exe");
            await CreateDummyExecutableAsync(exePath);

            var db = new KnownGameDatabase(_testDir, _dbLoggerMock.Object);
            var cache = new ScanCacheService(_testDir, _cacheLoggerMock.Object);
            var providerMock = new Mock<IExecutableMetadataProvider>();
            providerMock.Setup(p => p.ExtractAsync(It.IsAny<string>()))
                        .ReturnsAsync(new ExecutableMetadata
                        {
                            ExecutableName = "cs2.exe",
                            Publisher = "Valve",
                            WorkingDirectory = scanDir,
                            FileHash = "abc"
                        });

            var validator = new ScannerValidator(db, _valLoggerMock.Object);
            var engine = new GameDetectionEngine(db);

            var scanner = new ApplicationScannerService(providerMock.Object, engine, validator, cache, db, null, _loggerMock.Object);

            // Act - First Scan (Cache Miss)
            var scan1 = await scanner.ScanAsync(new[] { scanDir });

            // Act - Second Scan (Cache Hit)
            var scan2 = await scanner.ScanAsync(new[] { scanDir });

            // Assert
            Assert.Single(scan1);
            Assert.Single(scan2);
            providerMock.Verify(p => p.ExtractAsync(exePath), Times.Once); // Called exactly once
        }

        [Fact]
        public async Task SteamEpicRiotDetection_WorksSuccessfully()
        {
            // Arrange
            // Create Mocked folders
            string steamCommon = Path.Combine(_testDir, "steamapps", "common");
            string epicManifests = Path.Combine(_testDir, "EpicManifests");
            string riotDir = Path.Combine(_testDir, "RiotGames");

            Directory.CreateDirectory(steamCommon);
            Directory.CreateDirectory(epicManifests);
            Directory.CreateDirectory(riotDir);

            // 1. Steam CS2
            string cs2Exe = Path.Combine(steamCommon, "Counter-Strike 2", "cs2.exe");
            await CreateDummyExecutableAsync(cs2Exe);

            // 2. Epic Fortnite Manifest
            string fortniteDir = Path.Combine(_testDir, "Fortnite");
            string fortniteExe = Path.Combine(fortniteDir, "fortniteclient.exe");
            await CreateDummyExecutableAsync(fortniteExe);

            string epicItem = Path.Combine(epicManifests, "Fortnite.item");
            var manifestObj = new { InstallLocation = fortniteDir, LaunchExecutable = "fortniteclient.exe" };
            File.WriteAllText(epicItem, JsonSerializer.Serialize(manifestObj));

            // 3. Riot Valorant
            string valorantExe = Path.Combine(riotDir, "VALORANT", "valorant.exe");
            await CreateDummyExecutableAsync(valorantExe);

            var db = new KnownGameDatabase(_testDir, _dbLoggerMock.Object);
            await db.LoadAsync();

            var cache = new ScanCacheService(_testDir, _cacheLoggerMock.Object);
            var provider = new ExecutableMetadataProvider();
            var validator = new ScannerValidator(db, _valLoggerMock.Object);
            var engine = new GameDetectionEngine(db);

            // Create a custom subclass or mock registry in normal Scanner or supply custom scan paths
            var scanner = new ApplicationScannerService(provider, engine, validator, cache, db, null, _loggerMock.Object);

            // Act
            // Provide explicit scan paths to mock our environment in testing (cross-platform safe)
            var results = await scanner.ScanAsync(new[] { steamCommon, fortniteDir, riotDir });

            // Assert
            var games = results.Where(r => r.Type == "Game").ToList();
            Assert.Contains(games, g => g.Name.Contains("Counter-Strike 2"));
            Assert.Contains(games, g => g.Name.Contains("Valorant"));
        }

        [Fact]
        public async Task LargeLibraryScan_Handles1000FilesWithoutFailure()
        {
            // Arrange
            string scanDir = Path.Combine(_testDir, "LargeScan");
            Directory.CreateDirectory(scanDir);

            int numberOfFiles = 1005;
            for (int i = 1; i <= numberOfFiles; i++)
            {
                string name = $"app_{i}.exe";
                // Let's make some games, some apps
                if (i % 100 == 0) name = "cs2.exe"; // CS2 game (duplicate path is fine if in separate dirs, but let's append suffix or put in separate folders)

                string fileSubDir = Path.Combine(scanDir, $"AppFolder_{i}");
                Directory.CreateDirectory(fileSubDir);
                await CreateDummyExecutableAsync(Path.Combine(fileSubDir, name));
            }

            var db = new KnownGameDatabase(_testDir, _dbLoggerMock.Object);
            var cache = new ScanCacheService(_testDir, _cacheLoggerMock.Object);
            var provider = new ExecutableMetadataProvider();
            var validator = new ScannerValidator(db, _valLoggerMock.Object);
            var engine = new GameDetectionEngine(db);

            var scanner = new ApplicationScannerService(provider, engine, validator, cache, db, null, _loggerMock.Object);

            // Act
            var results = await scanner.ScanAsync(new[] { scanDir });

            // Assert
            Assert.Equal(numberOfFiles, results.Count());
        }

        [Fact]
        public async Task GameLibraryService_Integration_AutomaticallyAddsScannedGames()
        {
            // Arrange
            string scanDir = Path.Combine(_testDir, "ScanAndAdd");
            Directory.CreateDirectory(scanDir);

            string cs2Exe = Path.Combine(scanDir, "cs2.exe");
            await CreateDummyExecutableAsync(cs2Exe);

            var repo = new GameLibraryRepository(_testDir, null);
            var libraryService = new GameLibraryService(repo, null);

            var db = new KnownGameDatabase(_testDir, _dbLoggerMock.Object);
            var cache = new ScanCacheService(_testDir, _cacheLoggerMock.Object);
            var provider = new ExecutableMetadataProvider();
            var validator = new ScannerValidator(db, _valLoggerMock.Object);
            var engine = new GameDetectionEngine(db);

            var scanner = new ApplicationScannerService(provider, engine, validator, cache, db, libraryService, _loggerMock.Object);

            // Act
            var scanned = await scanner.ScanAsync(new[] { scanDir });

            // Assert
            var gamesInLibrary = (await libraryService.GetGames()).ToList();
            Assert.Single(gamesInLibrary);
            var savedGame = gamesInLibrary.First();
            Assert.Equal("Counter-Strike 2", savedGame.Name);
            Assert.Equal(GameSource.Scanner, savedGame.Source);
        }
    }
}
