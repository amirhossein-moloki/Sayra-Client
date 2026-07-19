using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record CpuInformation
    {
        public string Name { get; init; }
        public string Vendor { get; init; }
        public string Architecture { get; init; }
        public int LogicalCores { get; init; }
        public int PhysicalCores { get; init; }
        public int Threads { get; init; }
        public double BaseClock { get; init; }
        public double CurrentClock { get; init; }
        public long L1Cache { get; init; }
        public long L2Cache { get; init; }
        public long L3Cache { get; init; }
        public string InstructionSets { get; init; }
        public bool VirtualizationSupport { get; init; }

        public CpuInformation(
            string Name,
            string Vendor,
            string Architecture,
            int LogicalCores,
            int PhysicalCores,
            int Threads,
            double BaseClock,
            double CurrentClock = 0.0,
            long L1Cache = 0,
            long L2Cache = 0,
            long L3Cache = 0,
            string InstructionSets = "Unknown",
            bool VirtualizationSupport = false)
        {
            this.Name = Name;
            this.Vendor = Vendor;
            this.Architecture = Architecture;
            this.LogicalCores = LogicalCores;
            this.PhysicalCores = PhysicalCores;
            this.Threads = Threads;
            this.BaseClock = BaseClock;
            this.CurrentClock = CurrentClock;
            this.L1Cache = L1Cache;
            this.L2Cache = L2Cache;
            this.L3Cache = L3Cache;
            this.InstructionSets = InstructionSets;
            this.VirtualizationSupport = VirtualizationSupport;
        }
    }
}
