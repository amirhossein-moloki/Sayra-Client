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
    }
}
