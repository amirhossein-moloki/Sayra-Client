using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record GpuInformation
    {
        public string Name { get; init; }
        public string Vendor { get; init; }
        public string DriverVersion { get; init; }
        public long DedicatedMemory { get; init; }
        public long SharedMemory { get; init; }
        public string PciBus { get; init; }
        public string VideoProcessor { get; init; }
        public string DriverDate { get; init; }
        public string CurrentResolution { get; init; }
        public double CurrentRefreshRate { get; init; }

        public GpuInformation(
            string Name,
            string Vendor,
            string DriverVersion,
            long DedicatedMemory,
            long SharedMemory,
            string PciBus = "Unknown",
            string VideoProcessor = "Unknown",
            string DriverDate = "Unknown",
            string CurrentResolution = "Unknown",
            double CurrentRefreshRate = 0.0)
        {
            this.Name = Name;
            this.Vendor = Vendor;
            this.DriverVersion = DriverVersion;
            this.DedicatedMemory = DedicatedMemory;
            this.SharedMemory = SharedMemory;
            this.PciBus = PciBus;
            this.VideoProcessor = VideoProcessor;
            this.DriverDate = DriverDate;
            this.CurrentResolution = CurrentResolution;
            this.CurrentRefreshRate = CurrentRefreshRate;
        }
    }
}
