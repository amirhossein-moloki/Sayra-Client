using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.GameLibrary.Models;
using Sayra.Client.GameLibrary.Services;
using Sayra.Client.Scanner.Cache;
using Sayra.Client.Scanner.Models;
using Sayra.Client.Scanner.Providers;
using Sayra.Client.Scanner.ScannerEngine;
using Sayra.Client.Scanner.Validation;

namespace Sayra.Client.Scanner.Services
{
    public interface IApplicationScannerService
    {
        Task<IEnumerable<DetectedApplication>> ScanAsync(
            IEnumerable<string>? customPaths = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }

    public class ScanProgress
    {
        public int TotalFiles { get; set; }
        public int ScannedFiles { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
    }

    public class ApplicationScannerService : IApplicationScannerService
    {
        private readonly IExecutableMetadataProvider _metadataProvider;
        private readonly IGameDetectionEngine _detectionEngine;
        private readonly IScannerValidator _validator;
        private readonly IScanCacheService _cacheService;
        private readonly IKnownGameDatabase _database;
        private readonly IGameLibraryService? _gameLibraryService;
        private readonly ILogger<ApplicationScannerService>? _logger;

        private readonly string[] _excludedDirs = new[]
        {
            "windows", "$recycle.bin", "system volume information", "temp", "cache",
            "appdata", "node_modules", "obj", "bin", ".git", "drivers", "microsoft", "system32"
        };

        public ApplicationScannerService(
            IExecutableMetadataProvider metadataProvider,
            IGameDetectionEngine detectionEngine,
            IScannerValidator validator,
            IScanCacheService cacheService,
            IKnownGameDatabase database,
            IGameLibraryService? gameLibraryService = null,
            ILogger<ApplicationScannerService>? logger = null)
        {
            _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
            _detectionEngine = detectionEngine ?? throw new ArgumentNullException(nameof(detectionEngine));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _gameLibraryService = gameLibraryService;
            _logger = logger;
        }

        public async Task<IEnumerable<DetectedApplication>> ScanAsync(
            IEnumerable<string>? customPaths = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Scanning started.");

            // 1. Ensure Cache and KnownGameDatabase are loaded
            await _cacheService.LoadAsync();
            await _database.LoadAsync();

            // 2. Discover files to scan
            var filesToScan = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Collect folders
            var pathsToScan = new List<string>();

            // Add launcher library paths
            AddLauncherPaths(pathsToScan);

            // Add Program Files folders
            AddProgramFilesPaths(pathsToScan);

            // Add Start Menu & Desktop
            AddShortcutsPaths(pathsToScan);

            // Add custom folders
            if (customPaths != null)
            {
                foreach (var path in customPaths)
                {
                    if (Directory.Exists(path))
                    {
                        pathsToScan.Add(path);
                    }
                }
            }

            // De-duplicate and scan directories for eligible files
            foreach (var rootPath in pathsToScan.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger?.LogInformation("Discovering files in: {Path}", rootPath);
                DiscoverFilesRecursively(rootPath, filesToScan, cancellationToken);
            }

            _logger?.LogInformation("Discovered {Count} potential files for verification and metadata extraction.", filesToScan.Count);

            // 3. Process discovered files
            var results = new ConcurrentBag<DetectedApplication>();
            int processedCount = 0;
            int totalCount = filesToScan.Count;

            var fileList = filesToScan.ToList();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount),
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(fileList, parallelOptions, async (filePath, ct) =>
            {
                ct.ThrowIfCancellationRequested();

                // Increment progress counter
                int currentProgress = Interlocked.Increment(ref processedCount);
                progress?.Report(new ScanProgress
                {
                    TotalFiles = totalCount,
                    ScannedFiles = currentProgress,
                    CurrentFile = Path.GetFileName(filePath)
                });

                // Check shortcut resolution
                string? targetPath = filePath;
                string ext = Path.GetExtension(filePath);
                if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
                {
                    targetPath = ShortcutParser.ResolveShortcut(filePath);
                    if (string.IsNullOrWhiteSpace(targetPath) || !targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                // Validate target path
                if (!_validator.Validate(targetPath))
                {
                    return;
                }

                // Check cache
                if (_cacheService.TryGet(targetPath, out var cachedApp) && cachedApp != null)
                {
                    results.Add(cachedApp);
                    _logger?.LogDebug("Cache Hit: {Path}", targetPath);
                    return;
                }

                // Extract metadata
                try
                {
                    var metadata = await _metadataProvider.ExtractAsync(targetPath);
                    _logger?.LogInformation("Metadata extracted for: {Path}", targetPath);

                    // Game detection Heuristics
                    var classification = _detectionEngine.Classify(metadata);

                    var detectedApp = new DetectedApplication
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = string.IsNullOrWhiteSpace(classification.DisplayName) ? Path.GetFileNameWithoutExtension(targetPath) : classification.DisplayName,
                        ExecutablePath = targetPath,
                        WorkingDirectory = metadata.WorkingDirectory,
                        Publisher = metadata.Publisher,
                        Version = metadata.ProductVersion,
                        Category = classification.Category,
                        Launcher = classification.Launcher,
                        ExecutableHash = metadata.FileHash,
                        Icon = metadata.ExecutableIcon,
                        Type = classification.Type,
                        ConfidenceScore = classification.ConfidenceScore
                    };

                    _cacheService.Set(targetPath, detectedApp);

                    if (classification.Type == "Game")
                    {
                        _logger?.LogInformation("Game detected: {Name} (Launcher: {Launcher})", detectedApp.Name, detectedApp.Launcher);
                    }
                    else if (classification.Type == "Application")
                    {
                        _logger?.LogInformation("Application detected: {Name}", detectedApp.Name);
                    }

                    results.Add(detectedApp);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to process metadata for {Path}", targetPath);
                }
            });

            // Save Cache
            await _cacheService.SaveAsync();

            _logger?.LogInformation("Scan completed. Total detected items: {Count}", results.Count);

            // 4. Populate GameLibraryService if integrated
            if (_gameLibraryService != null)
            {
                var currentGames = (await _gameLibraryService.GetGames()).ToList();
                foreach (var app in results.Where(r => r.Type == "Game"))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Check if game is already registered in Game Library (by executable path or name)
                    bool exists = currentGames.Any(g => g.ExecutablePath.Equals(app.ExecutablePath, StringComparison.OrdinalIgnoreCase) ||
                                                        g.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                    {
                        try
                        {
                            var newGame = new Game
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = app.Name,
                                ExecutablePath = app.ExecutablePath,
                                WorkingDirectory = app.WorkingDirectory,
                                Arguments = string.Empty,
                                IconPath = app.Icon,
                                Enabled = true,
                                Source = GameSource.Scanner,
                                Category = new GameCategory { Id = app.Category.ToLowerInvariant().Replace(" ", "-"), Name = app.Category }
                            };

                            await _gameLibraryService.AddGame(newGame);
                            _logger?.LogInformation("Automatically added newly discovered game '{Name}' to Game Library.", app.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Failed to register game {Name} to library", app.Name);
                        }
                    }
                    else
                    {
                        _logger?.LogDebug("Game '{Name}' already exists in Game Library. Duplicate ignored.", app.Name);
                    }
                }
            }

            return results.ToList();
        }

        private void DiscoverFilesRecursively(string dir, HashSet<string> files, CancellationToken cancellationToken)
        {
            try
            {
                if (!Directory.Exists(dir)) return;

                // Non-recursive level check to avoid deep scans of ignored systems
                string dirName = Path.GetFileName(dir).ToLowerInvariant();
                if (_excludedDirs.Any(ex => dirName.Contains(ex)))
                {
                    return;
                }

                // Add all executables and shortcuts in the current directory
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext == ".exe" || ext == ".lnk" || ext == ".url")
                        {
                            files.Add(file);
                        }
                    }
                }
                catch (UnauthorizedAccessException) { /* Ignore standard folders with access restriction */ }
                catch (Exception) { /* Skip error folders */ }

                // Recurse into subdirectories
                try
                {
                    foreach (var subDir in Directory.EnumerateDirectories(dir))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        DiscoverFilesRecursively(subDir, files, cancellationToken);
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (Exception) { }
            }
            catch (Exception) { }
        }

        private void AddLauncherPaths(List<string> paths)
        {
            // Steam
            string? steamPath = GetRegistryValue(@"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath") ??
                               GetRegistryValue(@"Software\Valve\Steam", "SteamPath");
            if (!string.IsNullOrWhiteSpace(steamPath) && Directory.Exists(steamPath))
            {
                string appsDir = Path.Combine(steamPath, "steamapps", "common");
                if (Directory.Exists(appsDir)) paths.Add(appsDir);
            }
            else
            {
                // Fallbacks
                var stdSteam = @"C:\Program Files (x86)\Steam\steamapps\common";
                if (Directory.Exists(stdSteam)) paths.Add(stdSteam);
            }

            // Epic Games Manifests
            string epicManifests = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
            if (Directory.Exists(epicManifests))
            {
                try
                {
                    foreach (var manifestFile in Directory.GetFiles(epicManifests, "*.item"))
                    {
                        try
                        {
                            string content = File.ReadAllText(manifestFile);
                            using var doc = JsonDocument.Parse(content);
                            if (doc.RootElement.TryGetProperty("InstallLocation", out var locProp))
                            {
                                string? loc = locProp.GetString();
                                if (!string.IsNullOrWhiteSpace(loc) && Directory.Exists(loc))
                                {
                                    paths.Add(loc);
                                    _logger?.LogInformation("Epic Games launcher folder found: {Path}", loc);
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            else
            {
                string stdEpic = @"C:\Program Files\Epic Games";
                if (Directory.Exists(stdEpic)) paths.Add(stdEpic);
            }

            // Riot Games
            string riotInstalls = @"C:\ProgramData\Riot Games\RiotClientInstalls.json";
            if (File.Exists(riotInstalls))
            {
                try
                {
                    string content = File.ReadAllText(riotInstalls);
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("rc_default", out var rcProp))
                    {
                        string? rcPath = rcProp.GetString();
                        if (!string.IsNullOrWhiteSpace(rcPath))
                        {
                            string? dir = Path.GetDirectoryName(rcPath);
                            if (dir != null && Directory.Exists(dir)) paths.Add(dir);
                        }
                    }
                }
                catch { }
            }
            else
            {
                string stdRiot = @"C:\Riot Games";
                if (Directory.Exists(stdRiot)) paths.Add(stdRiot);
            }

            // Ubisoft Connect
            string? uplayPath = GetRegistryValue(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher", "InstallDir");
            if (!string.IsNullOrWhiteSpace(uplayPath) && Directory.Exists(uplayPath))
            {
                string gamesDir = Path.Combine(uplayPath, "games");
                if (Directory.Exists(gamesDir)) paths.Add(gamesDir);
            }

            // EA Desktop
            string stdEa = @"C:\Program Files\Electronic Arts\EA Desktop\EA Desktop";
            if (Directory.Exists(stdEa)) paths.Add(stdEa);

            // GOG Galaxy
            string? gogPath = GetRegistryValue(@"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths", "client");
            if (!string.IsNullOrWhiteSpace(gogPath))
            {
                string? dir = Path.GetDirectoryName(gogPath);
                if (dir != null && Directory.Exists(dir)) paths.Add(dir);
            }

            // Battle.net
            string battleNet = @"C:\Program Files (x86)\Battle.net";
            if (Directory.Exists(battleNet)) paths.Add(battleNet);
        }

        private static void AddProgramFilesPaths(List<string> paths)
        {
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(pf) && Directory.Exists(pf)) paths.Add(pf);

            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(pf86) && Directory.Exists(pf86)) paths.Add(pf86);
        }

        private static void AddShortcutsPaths(List<string> paths)
        {
            // Desktop
            string pubDesk = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            if (!string.IsNullOrWhiteSpace(pubDesk) && Directory.Exists(pubDesk)) paths.Add(pubDesk);

            string userDesk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (!string.IsNullOrWhiteSpace(userDesk) && Directory.Exists(userDesk)) paths.Add(userDesk);

            // Start Menu
            string pubStart = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            if (!string.IsNullOrWhiteSpace(pubStart) && Directory.Exists(pubStart)) paths.Add(pubStart);

            string userStart = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            if (!string.IsNullOrWhiteSpace(userStart) && Directory.Exists(userStart)) paths.Add(userStart);
        }

        private static string? GetRegistryValue(string keyPath, string valueName)
        {
            if (!OperatingSystem.IsWindows()) return null;
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath) ??
                                Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath);
                return key?.GetValue(valueName)?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
