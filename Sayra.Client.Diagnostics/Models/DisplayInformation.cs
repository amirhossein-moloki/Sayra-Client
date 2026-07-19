using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record DisplayInformation
    {
        public string Resolution { get; init; }
        public double RefreshRate { get; init; }
        public bool Hdr { get; init; }
        public double Dpi { get; init; }
        public string MonitorName { get; init; }
        public string Manufacturer { get; init; }
        public string Orientation { get; init; }
        public bool IsPrimary { get; init; }

        public DisplayInformation(
            string Resolution,
            double RefreshRate,
            bool Hdr,
            double Dpi,
            string MonitorName = "Unknown",
            string Manufacturer = "Unknown",
            string Orientation = "Landscape",
            bool IsPrimary = false)
        {
            this.Resolution = Resolution;
            this.RefreshRate = RefreshRate;
            this.Hdr = Hdr;
            this.Dpi = Dpi;
            this.MonitorName = MonitorName;
            this.Manufacturer = Manufacturer;
            this.Orientation = Orientation;
            this.IsPrimary = IsPrimary;
        }
    }
}
