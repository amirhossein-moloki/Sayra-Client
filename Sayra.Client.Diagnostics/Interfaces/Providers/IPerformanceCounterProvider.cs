using System;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface IPerformanceCounterProvider
    {
        float GetCpuUsage();
        float GetRamUsage();
        float GetDiskUsage();
        float GetNextValue(string categoryName, string counterName, string instanceName = "");
    }
}
