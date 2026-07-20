using System.Threading;
using System.Threading.Tasks;

namespace SayraClient.Services;

public interface IWorkstationBackupService
{
    Task<string> CreateBackupAsync(string destinationPath, string? password = null, CancellationToken cancellationToken = default);
    Task<bool> RestoreBackupAsync(string backupPath, string? password = null, CancellationToken cancellationToken = default);
    Task<bool> ValidateBackupAsync(string backupPath, string? password = null, CancellationToken cancellationToken = default);
    string GetBackupChecksum(string backupPath);
}
