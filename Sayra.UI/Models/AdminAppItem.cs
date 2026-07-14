using CommunityToolkit.Mvvm.ComponentModel;

namespace Sayra.UI.Models
{
    public partial class AdminAppItem : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _type = "Game"; // "Game" or "Application"

        [ObservableProperty]
        private string _executablePath = string.Empty;

        [ObservableProperty]
        private string _arguments = string.Empty;

        [ObservableProperty]
        private string _workingDirectory = string.Empty;

        [ObservableProperty]
        private string _category = "General";

        [ObservableProperty]
        private string _status = "Installed";

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private string _launcherType = "Manual"; // "Manual", "Steam", "Epic Games", "Sayra Launcher"

        [ObservableProperty]
        private string _validationState = "Unverified"; // "Valid Path", "Path Not Found", "Unverified"

        [ObservableProperty]
        private string _source = "Manual"; // "Manual", "Steam", "Sayra Server"

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _runAsAdmin;

        [ObservableProperty]
        private bool _enableOverlay;

        [ObservableProperty]
        private bool _useSandbox;
    }
}
