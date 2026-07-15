using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Sayra.Client.Scanner.Providers
{
    public interface IExecutableMetadataProvider
    {
        Task<ExecutableMetadata> ExtractAsync(string filePath);
    }

    public class ExecutableMetadata
    {
        public string ExecutableName { get; set; } = string.Empty;
        public string FileVersion { get; set; } = string.Empty;
        public string ProductVersion { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public long ExecutableSize { get; set; }
        public string FileHash { get; set; } = string.Empty;
        public string ExecutableIcon { get; set; } = string.Empty; // Base64 or Path
        public DateTime CreationDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string WorkingDirectory { get; set; } = string.Empty;
    }

    public class ExecutableMetadataProvider : IExecutableMetadataProvider
    {
        public async Task<ExecutableMetadata> ExtractAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found for metadata extraction", filePath);
            }

            var info = new FileInfo(filePath);
            var metadata = new ExecutableMetadata
            {
                ExecutableName = info.Name,
                ExecutableSize = info.Length,
                CreationDate = info.CreationTimeUtc,
                ModifiedDate = info.LastWriteTimeUtc,
                WorkingDirectory = info.DirectoryName ?? string.Empty
            };

            // 1. Try extracting PE information via FileVersionInfo
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                metadata.FileVersion = versionInfo.FileVersion ?? string.Empty;
                metadata.ProductVersion = versionInfo.ProductVersion ?? string.Empty;
                metadata.Company = versionInfo.CompanyName ?? string.Empty;
                metadata.Publisher = versionInfo.CompanyName ?? string.Empty;
                metadata.ProductName = versionInfo.ProductName ?? string.Empty;
            }
            catch
            {
                // Fallbacks if not on Windows or if FileVersionInfo fails
                metadata.FileVersion = "1.0.0.0";
                metadata.ProductVersion = "1.0.0";
                metadata.Company = "Unknown";
                metadata.Publisher = "Unknown";
                metadata.ProductName = Path.GetFileNameWithoutExtension(filePath);
            }

            // Fill standard defaults if they are blank in FileVersionInfo
            if (string.IsNullOrWhiteSpace(metadata.Publisher)) metadata.Publisher = "Unknown";
            if (string.IsNullOrWhiteSpace(metadata.Company)) metadata.Company = "Unknown";
            if (string.IsNullOrWhiteSpace(metadata.ProductName)) metadata.ProductName = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(metadata.FileVersion)) metadata.FileVersion = "1.0.0.0";
            if (string.IsNullOrWhiteSpace(metadata.ProductVersion)) metadata.ProductVersion = "1.0.0";

            // 2. Compute SHA256 file hash asynchronously
            try
            {
                metadata.FileHash = await ComputeSha256Async(filePath);
            }
            catch
            {
                metadata.FileHash = string.Empty;
            }

            // 3. Try to extract associated icon (safe platform check)
            metadata.ExecutableIcon = ExtractIconSafe(filePath);

            return metadata;
        }

        private static async Task<string> ComputeSha256Async(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            using var sha256 = SHA256.Create();
            byte[] hashBytes = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private static string ExtractIconSafe(string filePath)
        {
            // Avoid crashes on non-Windows platforms
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    return filePath;
                }
                catch
                {
                    // Fallback
                }
            }
            return string.Empty;
        }
    }
}
