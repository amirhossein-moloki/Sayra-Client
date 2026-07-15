namespace Sayra.Client.Launcher.Services;

public interface ISessionStateProvider
{
    bool IsSessionActive();
    bool IsWhitelisted(string executablePath);
}
