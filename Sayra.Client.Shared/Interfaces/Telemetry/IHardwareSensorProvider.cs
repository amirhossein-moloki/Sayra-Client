namespace Sayra.Client.Shared.Interfaces
{
    public interface IHardwareSensorProvider
    {
        bool IsAvailable { get; }
        double GetCpuTemperature();
        double GetGpuTemperature();
        double GetFanSpeed();
    }
}
