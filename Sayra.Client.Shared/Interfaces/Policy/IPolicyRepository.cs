using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IPolicyRepository
    {
        Task SavePolicyAsync(PolicyProfile profile, CancellationToken cancellationToken = default);
        Task<List<PolicyProfile>> LoadPoliciesAsync(CancellationToken cancellationToken = default);
        Task<long> GetPolicyVersionAsync(CancellationToken cancellationToken = default);
        Task DeletePolicyAsync(string policyId, CancellationToken cancellationToken = default);
        Task<List<PolicyProfile>> GetActivePoliciesAsync(CancellationToken cancellationToken = default);
    }
}
