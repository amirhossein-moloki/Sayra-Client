using System;
using System.IO;
using System.Linq;

namespace Sayra.Client.Shared.Fleet.Security
{
    /// <summary>
    /// Interface for validating paths and protecting against traversal, system directory access, and symlinks.
    /// </summary>
    public interface ISecurePathValidator
    {
        /// <summary>
        /// Validates that a path is safe, within allowed areas, and not subject to path traversal/symlink attacks.
        /// </summary>
        bool IsSafePath(string path);

        /// <summary>
        /// Validates and returns the canonical safe path, or throws an exception.
        /// </summary>
        string ValidateAndCanonicalize(string path);
    }

    /// <summary>
    /// Implements secure path validation to protect against path traversal, symbolic links, and system folder access.
    /// </summary>
    public class SecurePathValidator : ISecurePathValidator
    {
        private static readonly string[] BlockedDirectoryKeywords = new[]
        {
            "System32", "SysWOW64", "Windows", "WinSxS", "system.ini", "win.ini", "etc", "passwd", "shadow", "proc", "sys", "boot"
        };

        private readonly string _allowedRoot;

        /// <summary>
        /// Initializes a new instance of SecurePathValidator.
        /// </summary>
        public SecurePathValidator()
        {
            // Restrict file operations to the application's base directory or standard safe data subdirectory.
            string baseRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data"));
            if (!Directory.Exists(baseRoot))
            {
                Directory.CreateDirectory(baseRoot);
            }
            // Ensure trailing directory separator to prevent prefix-matching path traversal bypasses (e.g. Data vs DataSecret)
            _allowedRoot = baseRoot.EndsWith(Path.DirectorySeparatorChar) ? baseRoot : baseRoot + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Validates that a path is safe.
        /// </summary>
        public bool IsSafePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            try
            {
                // 1. Path Traversal & Normalization check
                string fullPath = Path.GetFullPath(path);

                // Check for direct "../" or "..\" patterns in raw path to prevent traversal bypass
                if (path.Contains("..") || path.Contains("/../") || path.Contains("\\..\\"))
                {
                    return false;
                }

                // 2. Sandbox/Allowed Root Directory containment validation
                string checkPath = fullPath;
                if (Directory.Exists(fullPath) && !fullPath.EndsWith(Path.DirectorySeparatorChar))
                {
                    checkPath += Path.DirectorySeparatorChar;
                }

                if (!checkPath.StartsWith(_allowedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // 3. System Directory Protection (block critical system paths)
                foreach (var keyword in BlockedDirectoryKeywords)
                {
                    if (fullPath.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false;
                    }
                }

                // 4. Symbolic Link / Reparse Point Attack Mitigation
                var info = new DirectoryInfo(fullPath);
                var current = info;
                while (current != null)
                {
                    if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    {
                        return false; // Symbolic link/reparse point detected - potential attack!
                    }
                    current = current.Parent;
                }

                if (File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    if ((fileInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Canonicalizes and validates path, throwing UnauthorizedAccessException if unsafe.
        /// </summary>
        public string ValidateAndCanonicalize(string path)
        {
            if (!IsSafePath(path))
            {
                throw new UnauthorizedAccessException($"Access to path '{path}' is denied. Safe validation failed.");
            }
            return Path.GetFullPath(path);
        }
    }
}
