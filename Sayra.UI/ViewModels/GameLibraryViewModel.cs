using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
            // Populate with mock GameItems to demonstrate structure and dynamic capability
            Games.Add(new GameItem
            {
                Id = "1",
                Title = "CS:GO 2",
                Category = "Shooter",
                Description = "Tactical shooter game."
            });
            Games.Add(new GameItem
            {
                Id = "2",
                Title = "Dota 2",
                Category = "MOBA",
                Description = "Multiplayer online battle arena."
            });
            Games.Add(new GameItem
            {
                Id = "3",
                Title = "Cyberpunk 2077",
                Category = "RPG",
                Description = "Action role-playing video game."
            });
            Games.Add(new GameItem
            {
                Id = "4",
                Title = "FIFA 24",
                Category = "Sports",
                Description = "Association football simulation game."
            });
        }
    }
}
