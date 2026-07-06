using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class BackupService
{
    private readonly ILogger<BackupService> _logger;
    private readonly string _baseDir = AppContext.BaseDirectory;
    private readonly string _backupDir;

    public BackupService(ILogger<BackupService> logger)
    {
        _logger = logger;
        _backupDir = Path.Combine(_baseDir, "Backups");
    }

    public async Task<string?> CreateBackupAsync(string version)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string currentBackupPath = Path.Combine(_backupDir, $"{version}_{timestamp}");

        try
        {
            _logger.LogInformation("Creating backup at: {Path}", currentBackupPath);

            if (!Directory.Exists(currentBackupPath))
            {
                Directory.CreateDirectory(currentBackupPath);
            }

            // Backup binaries and subdirectories (except Backups and logs)
            foreach (var file in Directory.GetFiles(_baseDir))
            {
                string fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(currentBackupPath, fileName), true);
            }

            foreach (var dir in Directory.GetDirectories(_baseDir))
            {
                string dirName = Path.GetFileName(dir);
                if (dirName.Equals("Backups", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("logs", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("Updates", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                CopyDirectory(dir, Path.Combine(currentBackupPath, dirName));
            }

            _logger.LogInformation("Backup created successfully.");
            return currentBackupPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup.");
            return null;
        }
    }

    public bool Rollback(string backupPath)
    {
        try
        {
            _logger.LogWarning("Initiating rollback from: {Path}", backupPath);
            if (!Directory.Exists(backupPath))
            {
                _logger.LogError("Rollback failed: Backup directory not found.");
                return false;
            }

            // In a real scenario, the Updater utility would handle this while the service is stopped.
            // This method might be called by the Updater.

            CopyDirectory(backupPath, _baseDir);

            _logger.LogInformation("Rollback completed successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed.");
            return false;
        }
    }

    private void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string dest = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }

        foreach (string folder in Directory.GetDirectories(sourceDir))
        {
            string dest = Path.Combine(destinationDir, Path.GetFileName(folder));
            CopyDirectory(folder, dest);
        }
    }
}
