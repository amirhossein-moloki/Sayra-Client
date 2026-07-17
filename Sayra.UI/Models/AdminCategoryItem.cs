using CommunityToolkit.Mvvm.ComponentModel;

namespace Sayra.UI.Models
{
    public partial class AdminCategoryItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _iconGeometry = string.Empty;

        [ObservableProperty]
        private int _count;

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private bool _isBrandIcon = false;
    }
}
