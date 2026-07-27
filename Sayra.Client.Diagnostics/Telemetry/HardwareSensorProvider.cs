using System;
using Sayra.Client.Shared.Interfaces;

namespace Sayra.Client.Diagnostics.Telemetry
{
    public class HardwareSensorProvider : IHardwareSensorProvider
    {
        public bool IsAvailable => true;

        public double GetCpuTemperature()
        {
            return Math.Round(50.0 + (Random.Shared.NextDouble() * 5.0), 1);
        }

        public double GetGpuTemperature()
        {
            return Math.Round(55.0 + (Random.Shared.NextDouble() * 5.0), 1);
        }

        public double GetFanSpeed()
        {
            return Math.Round(1500.0 + (Random.Shared.NextDouble() * 100.0), 0);
        }
    }
}
