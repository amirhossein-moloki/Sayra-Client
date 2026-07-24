using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.LocalAdmin.Storage
{
    public class ClientConfigurationRepository : IClientConfigurationRepository
    {
        private readonly string _basePath;
        private readonly string _filePath;
        private readonly string _backupPath;
        private readonly ILogger<ClientConfigurationRepository>? _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static readonly byte[] EntropySalt = System.Text.Encoding.UTF8.GetBytes("SAYRA_Enterprise_Hardened_Salt_Vector_38294=");

        public ClientConfigurationRepository(string? basePath = null, ILogger<ClientConfigurationRepository>? logger = null)
        {
            _basePath = basePath != null
                ? Path.Combine(basePath, "Configuration")
                : Path.Combine(AppContext.BaseDirectory, "Data", "Configuration");
            _filePath = Path.Combine(_basePath, "client_config.json");
            _backupPath = Path.Combine(_basePath, "client_config.json.bak");
            _logger = logger;
        }

        private byte[] Encrypt(byte[] plaintextBytes)
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var asm = System.Reflection.Assembly.Load("System.Security.Cryptography.ProtectedData");
                    var type = asm.GetType("System.Security.Cryptography.ProtectedData");
                    var scopeType = asm.GetType("System.Security.Cryptography.DataProtectionScope");
                    var localMachineScope = Enum.Parse(scopeType!, "LocalMachine");

                    var protectMethod = type!.GetMethod("Protect", new[] { typeof(byte[]), typeof(byte[]), scopeType! });
                    return (byte[])protectMethod!.Invoke(null, new object[] { plaintextBytes, EntropySalt, localMachineScope })!;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "DPAPI Protect via reflection failed under Windows. Using soft protection fallback.");
                }
            }

            // Fallback for non-Windows and test environments
            var result = new byte[plaintextBytes.Length];
            var machineHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Environment.MachineName));
            for (int i = 0; i < plaintextBytes.Length; i++)
            {
                byte entropyByte = EntropySalt[i % EntropySalt.Length];
                byte machineByte = machineHash[i % machineHash.Length];
                result[i] = (byte)(plaintextBytes[i] ^ entropyByte ^ machineByte);
            }
            return result;
        }

        private byte[] Decrypt(byte[] ciphertextBytes)
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var asm = System.Reflection.Assembly.Load("System.Security.Cryptography.ProtectedData");
                    var type = asm.GetType("System.Security.Cryptography.ProtectedData");
                    var scopeType = asm.GetType("System.Security.Cryptography.DataProtectionScope");
                    var localMachineScope = Enum.Parse(scopeType!, "LocalMachine");

                    var unprotectMethod = type!.GetMethod("Unprotect", new[] { typeof(byte[]), typeof(byte[]), scopeType! });
                    return (byte[])unprotectMethod!.Invoke(null, new object[] { ciphertextBytes, EntropySalt, localMachineScope })!;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "DPAPI Unprotect via reflection failed. Attempting soft unprotection fallback.");
                }
            }

            // Fallback for non-Windows and test environments (XOR is symmetric)
            var result = new byte[ciphertextBytes.Length];
            var machineHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Environment.MachineName));
            for (int i = 0; i < ciphertextBytes.Length; i++)
            {
                byte entropyByte = EntropySalt[i % EntropySalt.Length];
                byte machineByte = machineHash[i % machineHash.Length];
                result[i] = (byte)(ciphertextBytes[i] ^ entropyByte ^ machineByte);
            }
            return result;
        }

        public async Task<ClientConfiguration> LoadConfigurationAsync()
        {
            EnsureDirectoryExists();

            if (File.Exists(_filePath))
            {
                try
                {
                    var encryptedBytes = await File.ReadAllBytesAsync(_filePath);
                    var decryptedBytes = Decrypt(encryptedBytes);
                    using (var ms = new MemoryStream(decryptedBytes))
                    {
                        var result = await JsonSerializer.DeserializeAsync<ClientConfiguration>(ms, JsonOptions);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load configuration file {FilePath}. Attempting recovery from backup.", _filePath);
                }
            }

            if (File.Exists(_backupPath))
            {
                try
                {
                    var encryptedBytes = await File.ReadAllBytesAsync(_backupPath);
                    var decryptedBytes = Decrypt(encryptedBytes);
                    using (var ms = new MemoryStream(decryptedBytes))
                    {
                        var result = await JsonSerializer.DeserializeAsync<ClientConfiguration>(ms, JsonOptions);
                        if (result != null)
                        {
                            _logger?.LogInformation("Successfully recovered configuration from backup {BackupPath}", _backupPath);
                            try
                            {
                                File.Copy(_backupPath, _filePath, overwrite: true);
                            }
                            catch (Exception copyEx)
                            {
                                _logger?.LogWarning(copyEx, "Could not restore backup to {FilePath}.", _filePath);
                            }
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load configuration backup file {BackupPath} as well.", _backupPath);
                }
            }

            return new ClientConfiguration();
        }

        public async Task SaveConfigurationAsync(ClientConfiguration configuration)
        {
            EnsureDirectoryExists();

            string tempPath = _filePath + ".tmp";

            // 1. Write to temp file asynchronously with DPAPI encryption
            byte[] plaintextBytes;
            using (var ms = new MemoryStream())
            {
                await JsonSerializer.SerializeAsync(ms, configuration, JsonOptions);
                plaintextBytes = ms.ToArray();
            }

            byte[] encryptedBytes = Encrypt(plaintextBytes);
            await File.WriteAllBytesAsync(tempPath, encryptedBytes);

            // 2. Backup existing file before replacement
            if (File.Exists(_filePath))
            {
                try
                {
                    if (File.Exists(_backupPath))
                    {
                        File.Delete(_backupPath);
                    }
                    File.Copy(_filePath, _backupPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not create backup of configuration at {BackupPath}", _backupPath);
                }
            }

            // 3. Atomic replace
            try
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }
                File.Move(tempPath, _filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed atomic write for configuration. Falling back to copy.");
                if (File.Exists(tempPath))
                {
                    File.Copy(tempPath, _filePath, overwrite: true);
                    File.Delete(tempPath);
                }
            }
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
        }
    }
}
