using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IPolicyEngine
    {
        Task<PolicyChangeResult> ApplyPoliciesAsync(PolicyProfile profile, CancellationToken cancellationToken = default);
        Task<PolicyChangeResult> RemovePoliciesAsync(string policyId, CancellationToken cancellationToken = default);
        Task<PolicyChangeResult> UpdatePoliciesAsync(PolicyProfile profile, CancellationToken cancellationToken = default);
        Task<PolicyValidationResult> ValidatePoliciesAsync(PolicyProfile profile, CancellationToken cancellationToken = default);
        Task RollbackFailedPoliciesAsync(string policyId, CancellationToken cancellationToken = default);
    }
}
