namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.States
{
    public enum ProcessState
    {
        Created,
        Starting,
        Running,
        Stopping,
        Stopped,
        Crashed,
        Unknown
    }
}
