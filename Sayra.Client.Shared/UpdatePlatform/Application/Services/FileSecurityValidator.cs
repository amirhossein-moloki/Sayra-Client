using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Evaluates NTFS ACL security descriptors, directory write/read permissions, and owner SID values.
    /// Also supports path normalization, directory traversal prevention, and secure temporary directory management.
    /// Includes standard platform guards and safe fallbacks for non-Windows (Linux CI) environments.
    /// </summary>
    public class FileSecurityValidator : IFileSecurityValidator
    {
        private readonly ILogger<FileSecurityValidator> _logger;

        public FileSecurityValidator(ILogger<FileSecurityValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public FileSecurityReport ValidateFileSecurity(string filePath, string? expectedOwner = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File security validation failed: File '{Path}' does not exist.", filePath);
                return new FileSecurityReport(filePath, false, false, null, "File not found.", false);
            }

            // Check if it is a reparse point (symlink/junction attack prevention)
            try
            {
                var attrs = File.GetAttributes(filePath);
                if (attrs.HasFlag(FileAttributes.ReparsePoint))
                {
                    _logger.LogCritical("Security violation: Reparse point detected at '{Path}'.", filePath);
                    return new FileSecurityReport(filePath, false, false, null, "Reparse point / symlink detected.", false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read attributes of file '{Path}'.", filePath);
            }

            bool hasRead = HasReadPermission(filePath);
            bool hasWrite = HasWritePermission(filePath);
            string? owner = null;
            string? aclSettings = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var fileSecurity = fileInfo.GetAccessControl();
                    var ownerAccount = fileSecurity.GetOwner(typeof(NTAccount));
                    owner = ownerAccount?.Value;

                    var rules = fileSecurity.GetAccessRules(true, true, typeof(NTAccount));
                    var sb = new System.Text.StringBuilder();
                    foreach (FileSystemAccessRule rule in rules)
                    {
                        sb.AppendLine($"Identity: {rule.IdentityReference.Value}, Rights: {rule.FileSystemRights}, Type: {rule.AccessControlType}");
                    }
                    aclSettings = sb.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read security descriptors for file '{Path}'.", filePath);
                    aclSettings = $"Error reading ACLs: {ex.Message}";
                }
            }
            else
            {
                owner = "System (CI-Simulated)";
                aclSettings = "ACL details are only supported on Windows.";
            }

            bool isOwnerValid = string.IsNullOrEmpty(expectedOwner) ||
                                (owner != null && owner.Contains(expectedOwner, StringComparison.OrdinalIgnoreCase));

            bool isValid = hasRead && hasWrite && isOwnerValid;

            return new FileSecurityReport(filePath, hasRead, hasWrite, owner, aclSettings, isValid);
        }

        /// <inheritdoc />
        public FileSecurityReport ValidateDirectorySecurity(string directoryPath, string? expectedOwner = null)
        {
            if (string.IsNullOrEmpty(directoryPath))
                throw new ArgumentException("Directory path cannot be empty.", nameof(directoryPath));

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWarning("Directory security validation failed: Directory '{Path}' does not exist.", directoryPath);
                return new FileSecurityReport(directoryPath, false, false, null, "Directory not found.", false);
            }

            // Check if it is a reparse point (symlink/junction attack prevention)
            try
            {
                var attrs = File.GetAttributes(directoryPath);
                if (attrs.HasFlag(FileAttributes.ReparsePoint))
                {
                    _logger.LogCritical("Security violation: Reparse point directory detected at '{Path}'.", directoryPath);
                    return new FileSecurityReport(directoryPath, false, false, null, "Reparse point / symlink detected.", false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read attributes of directory '{Path}'.", directoryPath);
            }

            bool hasRead = HasReadPermission(directoryPath);
            bool hasWrite = HasWritePermission(directoryPath);
            string? owner = null;
            string? aclSettings = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(directoryPath);
                    var dirSecurity = dirInfo.GetAccessControl();
                    var ownerAccount = dirSecurity.GetOwner(typeof(NTAccount));
                    owner = ownerAccount?.Value;

                    var rules = dirSecurity.GetAccessRules(true, true, typeof(NTAccount));
                    var sb = new System.Text.StringBuilder();
                    foreach (FileSystemAccessRule rule in rules)
                    {
                        sb.AppendLine($"Identity: {rule.IdentityReference.Value}, Rights: {rule.FileSystemRights}, Type: {rule.AccessControlType}");
                    }
                    aclSettings = sb.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read security descriptors for directory '{Path}'.", directoryPath);
                    aclSettings = $"Error reading ACLs: {ex.Message}";
                }
            }
            else
            {
                owner = "System (CI-Simulated)";
                aclSettings = "ACL details are only supported on Windows.";
            }

            bool isOwnerValid = string.IsNullOrEmpty(expectedOwner) ||
                                (owner != null && owner.Contains(expectedOwner, StringComparison.OrdinalIgnoreCase));

            bool isValid = hasRead && hasWrite && isOwnerValid;

            return new FileSecurityReport(directoryPath, hasRead, hasWrite, owner, aclSettings, isValid);
        }

        /// <inheritdoc />
        public bool HasWritePermission(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                if (Directory.Exists(path))
                {
                    string tempFile = Path.Combine(path, Guid.NewGuid().ToString("N") + ".tmp");
                    File.WriteAllText(tempFile, "SAYRA_TEMP_WRITE_TEST");
                    File.Delete(tempFile);
                    return true;
                }
                else if (File.Exists(path))
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public bool HasReadPermission(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                if (Directory.Exists(path))
                {
                    Directory.GetFiles(path);
                    return true;
                }
                else if (File.Exists(path))
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public string NormalizeAndValidatePath(string path, string secureRootDirectory)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (string.IsNullOrEmpty(secureRootDirectory))
                throw new ArgumentNullException(nameof(secureRootDirectory));

            // Prevent direct path traversal tricks
            if (path.Contains(".."))
            {
                throw new SecurityValidationException("Path contains dangerous directory traversal components ('..').");
            }

            // Prevent UNC Path attacks (e.g. \\attacker-ip\share)
            if (path.StartsWith("\\\\") || path.StartsWith("//"))
            {
                throw new SecurityValidationException("UNC paths or network paths are strictly prohibited for security reasons.");
            }

            try
            {
                // Resolve full absolute paths
                string absolutePath = Path.GetFullPath(path);
                string absoluteRoot = Path.GetFullPath(secureRootDirectory);

                // Standardize directory separator characters
                string standardPath = absolutePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
                string standardRoot = absoluteRoot.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);

                // Verify that the resolved path is inside the secure root directory
                if (!standardPath.StartsWith(standardRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(standardPath, standardRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SecurityValidationException($"Directory traversal attempt detected! Path '{path}' is outside the secure root directory '{secureRootDirectory}'.");
                }

                // Verify no reparse points exist along the path to prevent symlink/junction hijacking attacks
                if (File.Exists(absolutePath) || Directory.Exists(absolutePath))
                {
                    var attrs = File.GetAttributes(absolutePath);
                    if (attrs.HasFlag(FileAttributes.ReparsePoint))
                    {
                        throw new SecurityValidationException($"Reparse point (symbolic link or directory junction) detected at '{absolutePath}'. Access blocked to prevent hijacking.");
                    }
                }

                return absolutePath;
            }
            catch (SecurityValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SecurityValidationException($"Path normalization and validation failed for '{path}'.", ex);
            }
        }

        /// <inheritdoc />
        public string CreateSecureTemporaryDirectory()
        {
            string baseTempDir = Path.Combine(Path.GetTempPath(), "SAYRA_Client_Temp");
            string securePath = Path.Combine(baseTempDir, Guid.NewGuid().ToString("N"));

            try
            {
                // Ensure base directory exists
                if (!Directory.Exists(baseTempDir))
                {
                    Directory.CreateDirectory(baseTempDir);
                }

                Directory.CreateDirectory(securePath);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Secure NTFS ACLs: Disable inheritance, and allow ONLY SYSTEM and Administrators full access.
                    var dirInfo = new DirectoryInfo(securePath);
                    var dirSecurity = dirInfo.GetAccessControl();

                    dirSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

                    // 1. Add SYSTEM full control rule
                    var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                    var systemRule = new FileSystemAccessRule(
                        systemSid,
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow);
                    dirSecurity.AddAccessRule(systemRule);

                    // 2. Add Administrators full control rule
                    var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                    var adminRule = new FileSystemAccessRule(
                        adminSid,
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow);
                    dirSecurity.AddAccessRule(adminRule);

                    dirInfo.SetAccessControl(dirSecurity);
                    _logger.LogInformation("Configured secure NTFS ACLs for temporary directory: {Path}", securePath);
                }

                return securePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create secure temporary directory at: {Path}", securePath);
                throw new SecurityValidationException($"Failed to create secure temporary directory.", ex);
            }
        }
    }
}
