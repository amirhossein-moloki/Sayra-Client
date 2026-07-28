using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Evaluates NTFS ACL security descriptors, directory write/read permissions, and owner SID values.
    /// </summary>
    public interface IFileSecurityValidator
    {
        /// <summary>
        /// Validates security permissions and ownership of a physical file.
        /// </summary>
        /// <param name="filePath">The file path to evaluate.</param>
        /// <param name="expectedOwner">The optional expected owner name or SID.</param>
        /// <returns>A FileSecurityReport representing the security analysis.</returns>
        FileSecurityReport ValidateFileSecurity(string filePath, string? expectedOwner = null);

        /// <summary>
        /// Validates security permissions and ownership of a directory.
        /// </summary>
        /// <param name="directoryPath">The directory path to evaluate.</param>
        /// <param name="expectedOwner">The optional expected owner name or SID.</param>
        /// <returns>A FileSecurityReport representing the security analysis.</returns>
        FileSecurityReport ValidateDirectorySecurity(string directoryPath, string? expectedOwner = null);

        /// <summary>
        /// Verifies if the current process has write access to the specified path.
        /// </summary>
        bool HasWritePermission(string path);

        /// <summary>
        /// Verifies if the current process has read access to the specified path.
        /// </summary>
        bool HasReadPermission(string path);
    }
}
