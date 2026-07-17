using CommunityToolkit.Mvvm.ComponentModel;

namespace Sayra.UI.Models
{
    public partial class GameItem : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _genre = string.Empty;

        [ObservableProperty]
        private string _imagePath = string.Empty;

        [ObservableProperty]
        private string _status = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isAvailable = true;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _logoImage = string.Empty;

        [ObservableProperty]
        private string _backgroundImage = string.Empty;

        [ObservableProperty]
        private string _launcher = string.Empty; // Steam, Epic, Battle.net, Riot, EA, Xbox, Ubisoft, Custom, GOG

        [ObservableProperty]
        private string _developer = string.Empty;

        [ObservableProperty]
        private string _releaseYear = string.Empty;

        [ObservableProperty]
        private string _executablePath = string.Empty;

        // Keep Category property pointing to Genre to preserve backward compatibility if any reference exists
        public string Category
        {
            get => Genre;
            set => Genre = value;
        }

        // Backward and visual alignment mappings
        public string CoverImage
        {
            get => ImagePath;
            set => ImagePath = value;
        }

        public string GenreAndYear => !string.IsNullOrEmpty(ReleaseYear) ? $"{Genre} • {ReleaseYear}" : Genre;
    }
}
