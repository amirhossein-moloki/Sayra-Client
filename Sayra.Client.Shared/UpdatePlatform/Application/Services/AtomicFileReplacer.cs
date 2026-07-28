using System;
using System.IO;
using System.Runtime.InteropServices;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Provides atomic file-level replacement using OS primitives to guarantee safe rollbacks and clean directory updates.
    /// Handles both Windows native ReplaceFile and platform-agnostic fallback paths.
    /// </summary>
    public class AtomicFileReplacer : IAtomicFileReplacer
    {
        #region Native Windows P/Invoke

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ReplaceFile(
            string lpReplacedFileName,
            string lpReplacementFileName,
            string? lpBackupFileName,
            uint dwReplaceFlags,
            IntPtr lpExclude,
            IntPtr lpReserved);

        #endregion

        /// <inheritdoc />
        public void ReplaceFile(string targetFilePath, string replacementFilePath, string? backupFilePath = null)
        {
            if (string.IsNullOrEmpty(targetFilePath))
                throw new ArgumentNullException(nameof(targetFilePath));
            if (string.IsNullOrEmpty(replacementFilePath))
                throw new ArgumentNullException(nameof(replacementFilePath));

            if (!File.Exists(replacementFilePath))
            {
                throw new FileNotFoundException("Replacement file does not exist.", replacementFilePath);
            }

            // Create target directory if it does not exist
            string? targetDirectory = Path.GetDirectoryName(targetFilePath);
            if (targetDirectory != null && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // If target file doesn't exist, we can't use Win32 ReplaceFile (which requires the replaced file to exist).
            // We just perform a standard safe copy or move.
            if (!File.Exists(targetFilePath))
            {
                try
                {
                    File.Copy(replacementFilePath, targetFilePath, overwrite: true);
                }
                catch (Exception ex)
                {
                    throw new AtomicReplacementException($"Failed to write new file {targetFilePath} atomically.", ex);
                }
                return;
            }

            // Target file exists. Ensure backup directory exists if backup path is specified.
            if (backupFilePath != null)
            {
                string? backupDirectory = Path.GetDirectoryName(backupFilePath);
                if (backupDirectory != null && !Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }
            }

            if (OperatingSystem.IsWindows())
            {
                // Native Win32 ReplaceFile
                // dwReplaceFlags = 0
                bool result = ReplaceFile(targetFilePath, replacementFilePath, backupFilePath, 0, IntPtr.Zero, IntPtr.Zero);
                if (!result)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new AtomicReplacementException($"Win32 ReplaceFile failed for {targetFilePath} with error code {errorCode}.");
                }
            }
            else
            {
                // Non-Windows platform fallback (CI environment)
                try
                {
                    if (backupFilePath != null)
                    {
                        if (File.Exists(backupFilePath))
                        {
                            File.Delete(backupFilePath);
                        }
                        File.Copy(targetFilePath, backupFilePath);
                    }

                    // Perform safe overwrite move
                    File.Move(replacementFilePath, targetFilePath, overwrite: true);
                }
                catch (Exception ex)
                {
                    throw new AtomicReplacementException($"Fallback atomic file replacement failed for {targetFilePath}.", ex);
                }
            }
        }

        /// <inheritdoc />
        public void ReplaceDirectoryContents(string sourceDir, string targetDir, string backupDir)
        {
            if (string.IsNullOrEmpty(sourceDir))
                throw new ArgumentNullException(nameof(sourceDir));
            if (string.IsNullOrEmpty(targetDir))
                throw new ArgumentNullException(nameof(targetDir));
            if (string.IsNullOrEmpty(backupDir))
                throw new ArgumentNullException(nameof(backupDir));

            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Source staging directory {sourceDir} does not exist.");
            }

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            try
            {
                // Traverse all files in the source staging directory
                foreach (string sourceFile in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(sourceDir, sourceFile);

                    // Skip configuration files from being replaced if they represent local persistent configuration.
                    // (We preserve client_config.json, SQLite databases .db, log files, etc.)
                    if (IsConfigurationOrPersistentFile(relativePath))
                    {
                        continue;
                    }

                    string targetFile = Path.Combine(targetDir, relativePath);
                    string backupFile = Path.Combine(backupDir, relativePath);

                    ReplaceFile(targetFile, sourceFile, backupFile);
                }
            }
            catch (Exception ex)
            {
                throw new AtomicReplacementException($"Atomic directory contents replacement from {sourceDir} to {targetDir} failed.", ex);
            }
        }

        private bool IsConfigurationOrPersistentFile(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return false;

            string normalized = relativePath.Replace('\\', '/').ToLowerInvariant();

            return normalized.Contains("client_config.json") ||
                   normalized.Contains("db_key.bin") ||
                   normalized.EndsWith(".db") ||
                   normalized.Contains("databases/") ||
                   normalized.Contains("logs/") ||
                   normalized.EndsWith(".log");
        }
    }
}
