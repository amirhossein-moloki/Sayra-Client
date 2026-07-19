using System;
using System.Collections.Generic;

namespace Sayra.Client.Diagnostics.Models
{
    public record MemoryInformation
    {
        public long InstalledMemory { get; init; }
        public long AvailableMemory { get; init; }
        public string MemoryType { get; init; }
        public int Speed { get; init; }
        public long UsedMemory { get; init; }
        public int SlotCount { get; init; }
        public bool EccSupport { get; init; }
        public List<MemoryModule> Modules { get; init; }

        public MemoryInformation(
            long InstalledMemory,
            long AvailableMemory,
            string MemoryType,
            int Speed,
            long UsedMemory = 0,
            int SlotCount = 0,
            bool EccSupport = false,
            List<MemoryModule>? Modules = null)
        {
            this.InstalledMemory = InstalledMemory;
            this.AvailableMemory = AvailableMemory;
            this.MemoryType = MemoryType;
            this.Speed = Speed;
            this.UsedMemory = UsedMemory;
            this.SlotCount = SlotCount;
            this.EccSupport = EccSupport;
            this.Modules = Modules ?? new List<MemoryModule>();
        }
    }
}
