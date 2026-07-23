namespace Sayra.Client.Shared.Interfaces
{
    public interface ISessionContextProvider
    {
        string? CurrentSessionId { get; }
    }
}
