using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sayra.UI.Models;

namespace Sayra.UI.ViewModels
{
    public partial class GameLibraryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _searchText = string.Empty;

        public ObservableCollection<GameItem> Games { get; } = new();

        public GameLibraryViewModel()
        {
            // Populate with mock GameItems to demonstrate premium gaming design
            Games.Add(new GameItem
            {
                Id = "1",
                Title = "VALORANT",
                Genre = "FPS",
                Status = "Available",
                ImagePath = "pack://application:,,,/Assets/Games/Valorant.jpg",
                IsAvailable = true,
                IsSelected = true,
                Description = "Tactical shooter game with unique agent abilities."
            });
            Games.Add(new GameItem
            {
                Id = "2",
                Title = "FORTNITE",
                Genre = "Battle Royale",
                Status = "Available",
                ImagePath = "pack://application:,,,/Assets/Games/Fortnite.jpg",
                IsAvailable = true,
                IsSelected = false,
                Description = "A fast-paced battle royale game with building mechanics."
            });
            Games.Add(new GameItem
            {
                Id = "3",
                Title = "CYBERPUNK 2077",
                Genre = "RPG",
                Status = "Available",
                ImagePath = "pack://application:,,,/Assets/Games/Cyberpunk.jpg",
                IsAvailable = true,
                IsSelected = false,
                Description = "Open-world, action-adventure story in Night City."
            });
            Games.Add(new GameItem
            {
                Id = "4",
                Title = "CS:GO 2",
                Genre = "Shooter",
                Status = "Available",
                ImagePath = "pack://application:,,,/Assets/Games/CsGo.jpg",
                IsAvailable = true,
                IsSelected = false,
                Description = "Tactical first-person shooter."
            });
            Games.Add(new GameItem
            {
                Id = "5",
                Title = "DOTA 2",
                Genre = "MOBA",
                Status = "Unavailable",
                ImagePath = "pack://application:,,,/Assets/Games/Dota2.jpg",
                IsAvailable = false,
                IsSelected = false,
                Description = "Multiplayer online battle arena."
            });
            Games.Add(new GameItem
            {
                Id = "6",
                Title = "FIFA 24",
                Genre = "Sports",
                Status = "Available",
                ImagePath = "pack://application:,,,/Assets/Games/Fifa24.jpg",
                IsAvailable = true,
                IsSelected = false,
                Description = "Association football simulation game."
            });
        }

        [RelayCommand]
        private void PlayGame(GameItem? game)
        {
            if (game == null) return;

            // Simple visual feedback: select the clicked game
            foreach (var g in Games)
            {
                g.IsSelected = (g.Id == game.Id);
            }

            Debug.WriteLine($"Playing game: {game.Title}");
        }
    }
}
