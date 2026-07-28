using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Evaluates Windows Authenticode signatures on assemblies, DLLs, and executable binaries prior to update installation.
    /// </summary>
    public interface IAuthenticodeVerifier
    {
        /// <summary>
        /// Validates the Authenticode signature of the specified binary file.
        /// </summary>
        /// <param name="filePath">The absolute path to the file to verify.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A security validation result.</returns>
        Task<SecurityValidationResult> VerifyFileAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
