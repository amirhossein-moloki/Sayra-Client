using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Sayra.UI.Models;

namespace Sayra.UI.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _sessionTime = "00:58:16";

        [ObservableProperty]
        private string _pcName = "PC-08";

        [ObservableProperty]
        private string _currentTime = DateTime.Now.ToString("HH:mm");

        [ObservableProperty]
        private string _currentDate = "1405/04/15";

        [ObservableProperty]
        private string _totalCost = "110,000 تومان";

        [ObservableProperty]
        private string _hourlyRate = "120,000 تومان";

        public ObservableCollection<GameModel> Games { get; } = new();
        public ObservableCollection<SystemInfoModel> SystemSpecs { get; } = new();

        public DashboardViewModel()
        {
            LoadMockData();
        }

        private void LoadMockData()
        {
            Games.Add(new GameModel { Title = "Valorant", Category = "Shooter" });
            Games.Add(new GameModel { Title = "Fortnite", Category = "Action" });
            Games.Add(new GameModel { Title = "Elden Ring", Category = "RPG" });
            Games.Add(new GameModel { Title = "GTA V", Category = "Action" });
            Games.Add(new GameModel { Title = "Cyberpunk 2077", Category = "RPG" });

            SystemSpecs.Add(new SystemInfoModel { Label = "CPU", Value = "Intel i7 13500F" });
            SystemSpecs.Add(new SystemInfoModel { Label = "GPU", Value = "RTX 4090" });
            SystemSpecs.Add(new SystemInfoModel { Label = "RAM", Value = "32GB" });
            SystemSpecs.Add(new SystemInfoModel { Label = "DISPLAY", Value = "4K OLED" });
        }
    }
}
