using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Sayra.UI.Models;

namespace Sayra.UI.ViewModels
{
    public partial class HardwarePanelViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _pcName = "PC-08";

        public ObservableCollection<HardwareInfo> HardwareItems { get; } = new();

        public HardwarePanelViewModel()
        {
            // Populate with exact data required by instructions:
            // CPU: Intel Core i7-13500F
            // GPU: NVIDIA RTX 4090
            // RAM: 32 GB DDR5
            // DISPLAY: 27" 4K OLED\n3840×2160\n144Hz

            HardwareItems.Add(new HardwareInfo
            {
                Label = "CPU",
                Value = "Intel Core i7-13500F",
                IconPathData = "M19 19H5V5H19V19M19 3H5C3.9 3 3 3.9 3 5V19C3 20.1 3.9 21 5 21H19C20.1 21 21 20.1 21 19V5C21 3.9 20.1 3 19 3M9 1H7V3H9V1M13 1H11V3H13V1M17 1H15V3H17V1M23 9H21V7H23V9M23 13H21V11H23V13M23 17H21V15H23V17M17 21H15V23H17V21M13 21H11V23H13V21M9 21H7V23H9V21M3 9H1V7H3V9M3 13H1V11H3V13M3 17H1V15H3V17Z",
                Temperature = 58.0,
                Usage = 34.2,
                Availability = "Active",
                Health = "Healthy"
            });

            HardwareItems.Add(new HardwareInfo
            {
                Label = "GPU",
                Value = "NVIDIA RTX 4090",
                IconPathData = "M19,4H5A2,2 0 0,0 3,6V18A2,2 0 0,0 5,20H19A2,2 0 0,0 21,18V6A2,2 0 0,0 19,4M19,18H5V12H19V18M19,10H5V6H19V10M8,8H6V7H8V8M12,8H10V7H12V8M16,8H14V7H16V8M8,16H6V14H8V16M12,16H10V14H12V16M16,16H14V14H16V16",
                Temperature = 62.5,
                Usage = 48.0,
                Availability = "Active",
                Health = "Healthy"
            });

            HardwareItems.Add(new HardwareInfo
            {
                Label = "RAM",
                Value = "32 GB DDR5",
                IconPathData = "M2 6H22V14H2V6M5 8V12M8 8V12M11 8V12M14 8V12M17 8V12M20 8V12 M2 15H22V16H2",
                Temperature = 45.0,
                Usage = 68.4,
                Availability = "Allocated",
                Health = "Healthy"
            });

            HardwareItems.Add(new HardwareInfo
            {
                Label = "DISPLAY",
                Value = "27\" 4K OLED\n3840×2160\n144Hz",
                IconPathData = "M21,16H3V4H21M21,2H3C1.89,2 1,2.89 1,4V16A2,2 0 0,0 3,18H10V21H8V23H16V21H14V18H21A2,2 0 0,0 23,16V4C23,2.89 22.1,2 21,2Z",
                Temperature = 0.0,
                Usage = 0.0,
                Availability = "Active",
                Health = "Healthy"
            });
        }
    }
}
