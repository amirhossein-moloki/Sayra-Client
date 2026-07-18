using CommunityToolkit.Mvvm.ComponentModel;

namespace Sayra.UI.Models
{
    public partial class AdminAppItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isChecked;

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _executable = string.Empty;

        [ObservableProperty]
        private string _category = string.Empty;

        [ObservableProperty]
        private string _gameType = string.Empty;

        [ObservableProperty]
        private string _launcher = string.Empty; // Steam, Epic, Battle.net, Riot, EA, Ubisoft, Xbox, Custom

        [ObservableProperty]
        private string _version = string.Empty;

        [ObservableProperty]
        private string _publisher = string.Empty;

        [ObservableProperty]
        private string _installationPath = string.Empty;

        [ObservableProperty]
        private string _installationSource = string.Empty;

        [ObservableProperty]
        private string _status = string.Empty; // Installed, Missing, Disabled, Updating, Corrupted, Validation Required

        [ObservableProperty]
        private string _lastUpdated = string.Empty;

        [ObservableProperty]
        private string _modifiedBy = string.Empty;

        [ObservableProperty]
        private string _size = string.Empty;

        [ObservableProperty]
        private string _license = string.Empty;

        [ObservableProperty]
        private string _iconGeometry = string.Empty;

        // Redesigned structured asset pipeline properties for game identity
        [ObservableProperty]
        private string _coverImage = string.Empty;

        [ObservableProperty]
        private string _logoImage = string.Empty;

        [ObservableProperty]
        private string _backgroundImage = string.Empty;

        [ObservableProperty]
        private string _releaseYear = string.Empty;

        [ObservableProperty]
        private string _arguments = "--nosplash --noborder --fullscreen";

        [ObservableProperty]
        private string _customTags = "Kiosk, GameNet, LAN";

        [ObservableProperty]
        private string _verificationStatus = "Validated";

        [ObservableProperty]
        private string _sandboxMode = "Enforced";

        [ObservableProperty]
        private bool _hideOnClient = false;

        // Structured wrapper properties to align with GameItem and Game
        public string Title
        {
            get => Name;
            set => Name = value;
        }

        public string Genre
        {
            get => GameType;
            set => GameType = value;
        }

        public string ImagePath
        {
            get => CoverImage;
            set => CoverImage = value;
        }

        public string Developer
        {
            get => Publisher;
            set => Publisher = value;
        }

        public string ExecutablePath
        {
            get
            {
                try
                {
                    return System.IO.Path.Combine(InstallationPath ?? string.Empty, Executable ?? string.Empty);
                }
                catch
                {
                    return Executable ?? string.Empty;
                }
            }
        }

        public string GenreAndYear => !string.IsNullOrEmpty(ReleaseYear) ? $"{Genre} • {ReleaseYear}" : Genre;
    }
}
