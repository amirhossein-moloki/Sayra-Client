using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Sayra.Client.Scanner.ScannerEngine;

namespace Sayra.Client.Scanner.Validation
{
    public interface IScannerValidator
    {
        bool Validate(string filePath);
    }

    public class ScannerValidator : IScannerValidator
    {
        private readonly IKnownGameDatabase _database;
        private readonly ILogger<ScannerValidator>? _logger;

        public ScannerValidator(IKnownGameDatabase database, ILogger<ScannerValidator>? logger = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _logger = logger;
        }

        public bool Validate(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger?.LogWarning("Validation failed: path is empty.");
                return false;
            }

            try
            {
                // 1. Exists
                if (!File.Exists(filePath))
                {
                    _logger?.LogWarning("Validation failed: file does not exist. {Path}", filePath);
                    return false;
                }

                // 2. Blacklisted
                string fileName = Path.GetFileName(filePath);
                if (_database.IsBlacklisted(fileName))
                {
                    _logger?.LogInformation("Validation: ignored blacklisted utility {Name}.", fileName);
                    return false;
                }

                // 3. Readable & Not corrupted (Check file length and MZ header if it's an executable)
                var info = new FileInfo(filePath);
                if (info.Length < 64)
                {
                    _logger?.LogWarning("Validation failed: file size too small. {Path}", filePath);
                    return false;
                }

                if (filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    int b1 = stream.ReadByte();
                    int b2 = stream.ReadByte();
                    if (b1 != 0x4D || b2 != 0x5A) // 'M' and 'Z'
                    {
                        _logger?.LogWarning("Validation failed: PE header signature 'MZ' not found. File may be corrupted or not a valid Windows executable. {Path}", filePath);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Validation failed due to exception for {Path}", filePath);
                return false;
            }
        }
    }
}
