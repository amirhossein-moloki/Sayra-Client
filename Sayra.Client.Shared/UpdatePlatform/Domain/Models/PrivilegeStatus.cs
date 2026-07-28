using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the current system execution privileges of the process.
    /// </summary>
    public class PrivilegeStatus
    {
        /// <summary>
        /// Gets a value indicating whether the current process execution environment possesses administrative rights.
        /// </summary>
        public bool IsAdministrator { get; }

        /// <summary>
        /// Gets a value indicating whether all required system security privileges are granted.
        /// </summary>
        public bool HasRequiredPrivileges { get; }

        /// <summary>
        /// Gets a value indicating whether the execution context is elevated.
        /// </summary>
        public bool IsElevated { get; }

        public PrivilegeStatus(bool isAdministrator, bool hasRequiredPrivileges, bool isElevated)
        {
            IsAdministrator = isAdministrator;
            HasRequiredPrivileges = hasRequiredPrivileges;
            IsElevated = isElevated;
        }
    }
}
