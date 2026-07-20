using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class WorkstationBackupService : IWorkstationBackupService
{
    private readonly ILogger<WorkstationBackupService> _logger;
    private readonly string _dataDir;

    public WorkstationBackupService(ILogger<WorkstationBackupService> logger)
    {
        _logger = logger;
        _dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
    }

    private string ResolvePassword(string? password)
    {
        if (!string.IsNullOrEmpty(password)) return password;
        return Environment.MachineName + "_SAYRA_SECURE_BACKUP_SALT_98234";
    }

    public async Task<string> CreateBackupAsync(string destinationPath, string? password = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting backup process...");

        if (!Directory.Exists(_dataDir))
        {
            Directory.CreateDirectory(_dataDir);
        }

        string tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");
        try
        {
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }

            _logger.LogInformation("Zipping data directory to temporary file: {TempZip}", tempZip);
            ZipFile.CreateFromDirectory(_dataDir, tempZip);

            _logger.LogInformation("Encrypting ZIP file using PBKDF2 derived key...");
            byte[] rawBytes = await File.ReadAllBytesAsync(tempZip, cancellationToken);
            byte[] encryptedBytes = EncryptBytes(rawBytes, ResolvePassword(password));

            string dir = Path.GetDirectoryName(destinationPath) ?? AppContext.BaseDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllBytesAsync(destinationPath, encryptedBytes, cancellationToken);

            string checksum = GetBackupChecksum(destinationPath);
            _logger.LogInformation("Backup created successfully at {Path}. Checksum: {Checksum}", destinationPath, checksum);

            return checksum;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create workstation backup.");
            throw;
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }
        }
    }

    public async Task<bool> RestoreBackupAsync(string backupPath, string? password = null, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Initiating backup restoration from: {Path}", backupPath);

        if (!File.Exists(backupPath))
        {
            _logger.LogError("Restore failed: Backup file not found.");
            return false;
        }

        string tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");
        try
        {
            _logger.LogInformation("Decrypting backup archive...");
            byte[] encryptedBytes = await File.ReadAllBytesAsync(backupPath, cancellationToken);
            byte[] decryptedBytes = DecryptBytes(encryptedBytes, ResolvePassword(password));

            await File.WriteAllBytesAsync(tempZip, decryptedBytes, cancellationToken);

            _logger.LogInformation("Validating decrypted ZIP file structure...");
            using (var archive = ZipFile.OpenRead(tempZip))
            {
                _logger.LogInformation("Archive validation passed. Contains {Count} entries.", archive.Entries.Count);
            }

            if (Directory.Exists(_dataDir))
            {
                _logger.LogInformation("Removing old configuration/database files before extraction...");
                Directory.Delete(_dataDir, true);
            }
            Directory.CreateDirectory(_dataDir);

            _logger.LogInformation("Extracting backup ZIP to {DataDir}...", _dataDir);
            ZipFile.ExtractToDirectory(tempZip, _dataDir);

            _logger.LogInformation("Restoration completed successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore workstation backup.");
            return false;
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }
        }
    }

    public async Task<bool> ValidateBackupAsync(string backupPath, string? password = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating backup archive at: {Path}", backupPath);
        if (!File.Exists(backupPath))
        {
            return false;
        }

        try
        {
            byte[] encryptedBytes = await File.ReadAllBytesAsync(backupPath, cancellationToken);
            byte[] decryptedBytes = DecryptBytes(encryptedBytes, ResolvePassword(password));

            string tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");
            try
            {
                await File.WriteAllBytesAsync(tempZip, decryptedBytes, cancellationToken);
                using (var archive = ZipFile.OpenRead(tempZip))
                {
                    return archive.Entries.Count >= 0;
                }
            }
            finally
            {
                if (File.Exists(tempZip))
                {
                    try { File.Delete(tempZip); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup validation failed (decryption or ZIP integrity error).");
            return false;
        }
    }

    public string GetBackupChecksum(string backupPath)
    {
        if (!File.Exists(backupPath)) return string.Empty;

        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(backupPath);
        byte[] hash = sha256.ComputeHash(stream);
        var sb = new StringBuilder();
        foreach (byte b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    private byte[] EncryptBytes(byte[] rawBytes, string password)
    {
        byte[] salt = new byte[16];
        RandomNumberGenerator.Fill(salt);

        using var deriveBytes = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        byte[] key = deriveBytes.GetBytes(32);
        byte[] iv = deriveBytes.GetBytes(16);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var ms = new MemoryStream();
        ms.Write(salt, 0, salt.Length); // prepend salt

        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(rawBytes, 0, rawBytes.Length);
            cs.FlushFinalBlock();
        }

        return ms.ToArray();
    }

    private byte[] DecryptBytes(byte[] encryptedBytes, string password)
    {
        if (encryptedBytes.Length < 16)
        {
            throw new CryptographicException("Invalid encrypted payload size.");
        }

        byte[] salt = new byte[16];
        Array.Copy(encryptedBytes, 0, salt, 0, 16);

        using var deriveBytes = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        byte[] key = deriveBytes.GetBytes(32);
        byte[] iv = deriveBytes.GetBytes(16);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(new MemoryStream(encryptedBytes, 16, encryptedBytes.Length - 16), aes.CreateDecryptor(), CryptoStreamMode.Read))
        {
            cs.CopyTo(ms);
        }

        return ms.ToArray();
    }
}
