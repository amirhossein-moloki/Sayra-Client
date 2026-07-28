using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Validates application and service health post-installation or post-rollback.
    /// </summary>
    public interface IRecoveryValidator
    {
        /// <summary>
        /// Runs a complete post-installation validation suite to verify if the installation succeeded and is healthy.
        /// </summary>
        Task<HealthValidationResult> ValidateHealthAsync(RecoveryContext context, CancellationToken cancellationToken = default);
    }
}
