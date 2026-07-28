using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Performs privilege, administrative rights, and UAC elevation checks.
    /// </summary>
    public interface IPrivilegeManager
    {
        /// <summary>
        /// Retrieves the current process administrative and privilege status.
        /// </summary>
        /// <returns>A PrivilegeStatus instance.</returns>
        PrivilegeStatus GetCurrentPrivilegeStatus();

        /// <summary>
        /// Ensures the process is executing with administrative privileges, throwing an exception if not.
        /// </summary>
        void EnsureAdminPrivileges();
    }
}
