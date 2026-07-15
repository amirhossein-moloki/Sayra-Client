using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Scanner.Models;

namespace Sayra.Client.Scanner.ScannerEngine
{
    public interface IKnownGameDatabase
    {
        Task LoadAsync();
        Task SaveAsync();
        bool IsBlacklisted(string executableName);
        bool IsLauncher(string executableName);
        KnownGameSignature? GetSignature(string executableName);
        Task AddSignatureAsync(KnownGameSignature signature);
        IEnumerable<KnownGameSignature> GetSignatures();
    }

    public class KnownGameDatabase : IKnownGameDatabase
    {
        private readonly string _filePath;
        private readonly ILogger<KnownGameDatabase>? _logger;
        private readonly List<KnownGameSignature> _signatures = new();
        private readonly HashSet<string> _blacklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "explorer.exe", "chrome.exe", "firefox.exe", "msedge.exe", "cmd.exe", "powershell.exe",
            "svchost.exe", "taskmgr.exe", "vlc.exe", "notepad.exe", "regedit.exe", "calc.exe",
            "control.exe", "rundll32.exe", "setup.exe", "install.exe", "unins000.exe", "uninstall.exe",
            "discord.exe", "spotify.exe", "zoom.exe", "teams.exe", "onedrive.exe", "dropbox.exe",
            "sayraclient.exe", "sayra.client.ui.exe", "sayra.ui.exe", "sayra.client.guardian.exe",
            "sayra.client.updater.exe"
        };
        private readonly HashSet<string> _launchers = new(StringComparer.OrdinalIgnoreCase)
        {
            "steam.exe", "epicgameslauncher.exe", "uplay.exe", "origin.exe", "galaxyclient.exe",
            "battle.net.exe", "riotclientux.exe", "riotclientservices.exe", "ea.exe", "eadesktop.exe"
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public KnownGameDatabase(string? basePath = null, ILogger<KnownGameDatabase>? logger = null)
        {
            string dir = basePath ?? Path.Combine(AppContext.BaseDirectory, "Data", "Scanner");
            _filePath = Path.Combine(dir, "known_games.json");
            _logger = logger;

            // Load default signatures
            PopulateDefaults();
        }

        private void PopulateDefaults()
        {
            _signatures.Add(new KnownGameSignature
            {
                ExecutableName = "cs2.exe",
                KnownPublisher = "Valve",
                DisplayName = "Counter-Strike 2",
                Category = "FPS",
                Launcher = "Steam"
            });
            _signatures.Add(new KnownGameSignature
            {
                ExecutableName = "valorant.exe",
                KnownPublisher = "Riot Games",
                DisplayName = "Valorant",
                Category = "Tactical Shooter",
                Launcher = "Riot Games"
            });
            _signatures.Add(new KnownGameSignature
            {
                ExecutableName = "leagueoflegends.exe",
                KnownPublisher = "Riot Games",
                DisplayName = "League of Legends",
                Category = "MOBA",
                Launcher = "Riot Games"
            });
            _signatures.Add(new KnownGameSignature
            {
                ExecutableName = "gta5.exe",
                KnownPublisher = "Rockstar Games",
                DisplayName = "Grand Theft Auto V",
                Category = "Action-Adventure",
                Launcher = "Epic Games"
            });
            _signatures.Add(new KnownGameSignature
            {
                ExecutableName = "witcher3.exe",
                KnownPublisher = "CD Projekt Red",
                DisplayName = "The Witcher 3: Wild Hunt",
                Category = "RPG",
                Launcher = "GOG Galaxy"
            });
        }

        public async Task LoadAsync()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    await SaveAsync(); // Persist defaults
                    return;
                }

                using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var loaded = await JsonSerializer.DeserializeAsync<List<KnownGameSignature>>(stream, JsonOptions);
                if (loaded != null && loaded.Count > 0)
                {
                    _signatures.Clear();
                    _signatures.AddRange(loaded);
                    _logger?.LogInformation("Loaded {Count} known game signatures from database.", _signatures.Count);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load known game signatures from {Path}", _filePath);
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_filePath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await JsonSerializer.SerializeAsync(stream, _signatures, JsonOptions);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save known game signatures to {Path}", _filePath);
            }
        }

        public bool IsBlacklisted(string executableName)
        {
            if (string.IsNullOrWhiteSpace(executableName)) return true;
            string name = Path.GetFileName(executableName);
            return _blacklist.Contains(name);
        }

        public bool IsLauncher(string executableName)
        {
            if (string.IsNullOrWhiteSpace(executableName)) return false;
            string name = Path.GetFileName(executableName);
            return _launchers.Contains(name);
        }

        public KnownGameSignature? GetSignature(string executableName)
        {
            if (string.IsNullOrWhiteSpace(executableName)) return null;
            string name = Path.GetFileName(executableName);
            return _signatures.Find(s => s.ExecutableName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public async Task AddSignatureAsync(KnownGameSignature signature)
        {
            if (signature == null) return;
            var existing = GetSignature(signature.ExecutableName);
            if (existing != null)
            {
                _signatures.Remove(existing);
            }
            _signatures.Add(signature);
            await SaveAsync();
        }

        public IEnumerable<KnownGameSignature> GetSignatures()
        {
            return _signatures;
        }
    }
}
