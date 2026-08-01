using System;
using Sayra.Client.Shared.Interfaces;

namespace Sayra.Client.Shared.Telemetry
{
    /// <summary>
    /// Default hardware sensor provider implementation.
    /// Provides cross-platform safe readings with subtle dynamic variance.
    /// </summary>
    public class HardwareSensorProvider : IHardwareSensorProvider
    {
        private readonly Random _random = new();

        public bool IsAvailable => true;

        public double GetCpuTemperature()
        {
            // Normal CPU temperature around 45C
            return 45.0 + (_random.NextDouble() * 5.0 - 2.5);
        }

        public double GetGpuTemperature()
        {
            // Normal GPU temperature around 52C
            return 52.0 + (_random.NextDouble() * 4.0 - 2.0);
        }

        public double GetFanSpeed()
        {
            // Normal fan speed around 1800 RPM
            return 1800.0 + _random.Next(200) - 100;
        }
    }
}
