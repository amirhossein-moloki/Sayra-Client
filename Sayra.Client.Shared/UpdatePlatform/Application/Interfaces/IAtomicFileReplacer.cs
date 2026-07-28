using System;
using System.IO;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Provides atomic file-level replacement using OS primitives to guarantee safe rollbacks and clean directory updates.
    /// </summary>
    public interface IAtomicFileReplacer
    {
        /// <summary>
        /// Atomically replaces a target file with a replacement file, optionallly creating a backup.
        /// </summary>
        /// <param name="targetFilePath">The destination file path to replace.</param>
        /// <param name="replacementFilePath">The source file path to write.</param>
        /// <param name="backupFilePath">The backup destination file path if the target file exists.</param>
        void ReplaceFile(string targetFilePath, string replacementFilePath, string? backupFilePath = null);

        /// <summary>
        /// Safely copies files from source to destination directory atomically, preserving existing configurations.
        /// </summary>
        /// <param name="sourceDir">Staging source directory.</param>
        /// <param name="targetDir">Active target production directory.</param>
        /// <param name="backupDir">Backup snapshot directory.</param>
        void ReplaceDirectoryContents(string sourceDir, string targetDir, string backupDir);
    }
}
