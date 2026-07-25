namespace SayraClient.Kiosk.Application.Interfaces;

public interface IShellProtectionService
{
    bool CheckShellState();
    bool DetectUnexpectedExplorer();
    void RestoreSayraShell();
    void RestoreExplorerShell();
}
