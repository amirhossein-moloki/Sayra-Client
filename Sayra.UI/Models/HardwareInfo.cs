using CommunityToolkit.Mvvm.ComponentModel;

namespace Sayra.UI.Models
{
    public partial class HardwareInfo : ObservableObject
    {
        [ObservableProperty]
        private string _label = string.Empty;

        [ObservableProperty]
        private string _value = string.Empty;

        [ObservableProperty]
        private string _iconPathData = string.Empty;

        // Prepared properties for future status values as requested
        [ObservableProperty]
        private double _temperature; // e.g. 65.5 (°C)

        [ObservableProperty]
        private double _usage; // e.g. 45.2 (%)

        [ObservableProperty]
        private string _availability = "Available"; // e.g. "Available", "Busy", "Idle"

        [ObservableProperty]
        private string _health = "Healthy"; // e.g. "Healthy", "Warning", "Critical"
    }
}
