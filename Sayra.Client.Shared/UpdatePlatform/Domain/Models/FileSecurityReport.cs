using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the security and permission evaluation report of a file or directory.
    /// </summary>
    public class FileSecurityReport
    {
        /// <summary>
        /// Gets the path of the verified file or directory.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets a value indicating whether read permission is granted.
        /// </summary>
        public bool HasReadPermission { get; }

        /// <summary>
        /// Gets a value indicating whether write permission is granted.
        /// </summary>
        public bool HasWritePermission { get; }

        /// <summary>
        /// Gets the owner of the file or directory.
        /// </summary>
        public string? Owner { get; }

        /// <summary>
        /// Gets a string representation of the ACL settings or rules.
        /// </summary>
        public string? AclSettings { get; }

        /// <summary>
        /// Gets a value indicating whether the overall security state is valid and meets requirements.
        /// </summary>
        public bool IsValid { get; }

        public FileSecurityReport(string path, bool hasReadPermission, bool hasWritePermission, string? owner, string? aclSettings, bool isValid)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            HasReadPermission = hasReadPermission;
            HasWritePermission = hasWritePermission;
            Owner = owner;
            AclSettings = aclSettings;
            IsValid = isValid;
        }
    }
}
