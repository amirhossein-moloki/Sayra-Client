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

        // Keep Category property pointing to Genre to preserve backward compatibility if any reference exists
        public string Category
        {
            get => Genre;
            set => Genre = value;
        }
    }
}
