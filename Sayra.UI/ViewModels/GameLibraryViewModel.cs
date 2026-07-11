using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sayra.UI.Models;
using Sayra.UI.Services;

namespace Sayra.UI.ViewModels
{
    public partial class GameLibraryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _searchText = string.Empty;

        private readonly List<GameItem> _allGames = new();

        public ObservableCollection<GameItem> Games { get; } = new();

        public GameLibraryViewModel()
        {
            Log("Constructor START");
            _ = LoadGamesAsync();
            Log("Constructor END");
        }

        private async Task LoadGamesAsync()
        {
            try
            {
                Log("Loading games asynchronously from MockGameService...");
                var service = new MockGameService();
                var list = await service.GetGamesAsync();

                _allGames.Clear();
                foreach (var game in list)
                {
                    _allGames.Add(game);
                }

                ApplyFilter();
                Log("Mock Games populated and filtered successfully");
            }
            catch (Exception ex)
            {
                Log($"Mock population failed: {ex}");
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string query = SearchText.Trim().ToLower();
            Games.Clear();
            foreach (var game in _allGames)
            {
                if (string.IsNullOrEmpty(query) ||
                    game.Title.ToLower().Contains(query) ||
                    game.Genre.ToLower().Contains(query))
                {
                    Games.Add(game);
                }
            }
        }

        [RelayCommand]
        private void PlayGame(GameItem? game)
        {
            if (game == null) return;

            // Update IsSelected in master list and active UI list
            foreach (var g in _allGames)
            {
                g.IsSelected = (g.Id == game.Id);
                // If currently playing, update status string
                if (g.IsSelected)
                {
                    g.Status = "Currently Playing";
                }
                else if (g.Status == "Currently Playing")
                {
                    g.Status = "Installed"; // revert back to Installed if deselected
                }
            }

            // Sync selection state to the filtered collection
            foreach (var g in Games)
            {
                g.IsSelected = (g.Id == game.Id);
                if (g.IsSelected)
                {
                    g.Status = "Currently Playing";
                }
                else if (g.Status == "Currently Playing")
                {
                    g.Status = "Installed";
                }
            }

            Debug.WriteLine($"Playing game: {game.Title}");
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formatted = $"[TRACE][GameLibraryViewModel][{timestamp}] {message}";
            System.Diagnostics.Debug.WriteLine(formatted);
            Console.WriteLine(formatted);
        }
    }
}
