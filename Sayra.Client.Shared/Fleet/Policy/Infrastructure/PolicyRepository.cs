using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Domain.Policy;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Fleet.Policy.Interfaces;

namespace Sayra.Client.Shared.Fleet.Policy.Infrastructure
{
    using DomainPolicyAssignment = Sayra.Client.Shared.Models.Phase9.Domain.Policy.PolicyAssignment;

    /// <summary>
    /// Thread-safe in-memory SQLite/Dictionary-backed implementation of the central policy repository.
    /// Supports complete transactional serialization, deep copying, and comprehensive queries.
    /// </summary>
    public class PolicyRepository : Sayra.Client.Shared.Fleet.Policy.Interfaces.IPolicyRepository
    {
        private readonly ConcurrentDictionary<string, PolicyDefinition> _policies = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PolicyVersion>> _versions = new();
        private readonly ConcurrentDictionary<string, DomainPolicyAssignment> _assignments = new();
        private readonly ConcurrentDictionary<string, List<PolicyHistory>> _history = new();
        private readonly ConcurrentDictionary<string, PolicyComplianceRecord> _compliance = new();

        private readonly SemaphoreSlim _lock = new(1, 1);

        /// <summary>
        /// Deep-clones a policy definition to maintain immutability boundaries.
        /// </summary>
        private PolicyDefinition ClonePolicy(PolicyDefinition original)
        {
            return original with
            {
                Rules = original.Rules.Select(r => r with
                {
                    Parameters = r.Parameters.Select(p => p with { }).ToList(),
                    Conditions = r.Conditions.Select(c => c with { }).ToList()
                }).ToList(),
                Metadata = original.Metadata with
                {
                    CustomAttributes = new Dictionary<string, string>(original.Metadata.CustomAttributes)
                }
            };
        }

        /// <summary>
        /// Deep-clones a policy version to maintain immutability boundaries.
        /// </summary>
        private PolicyVersion CloneVersion(PolicyVersion original)
        {
            return original with
            {
                Rules = original.Rules.Select(r => r with
                {
                    Parameters = r.Parameters.Select(p => p with { }).ToList(),
                    Conditions = r.Conditions.Select(c => c with { }).ToList()
                }).ToList()
            };
        }

        /// <summary>
        /// Deep-clones a policy compliance record to maintain immutability boundaries.
        /// </summary>
        private PolicyComplianceRecord CloneCompliance(PolicyComplianceRecord original)
        {
            return original with
            {
                Violations = original.Violations.Select(v => v with { }).ToList()
            };
        }

        /// <inheritdoc />
        public async Task<bool> SavePolicyAsync(PolicyDefinition policy, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var cloned = ClonePolicy(policy);
                _policies[policy.PolicyId] = cloned;
                return await Task.FromResult(true);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<PolicyDefinition?> GetPolicyAsync(string policyId, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (_policies.TryGetValue(policyId, out var policy))
                {
                    return await Task.FromResult(ClonePolicy(policy));
                }
                return await Task.FromResult<PolicyDefinition?>(null);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PolicyDefinition>> GetAllPoliciesAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var list = _policies.Values.Select(ClonePolicy).ToList();
                return await Task.FromResult<IReadOnlyList<PolicyDefinition>>(list);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeletePolicyAsync(string policyId, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                bool removed = _policies.TryRemove(policyId, out _);
                _versions.TryRemove(policyId, out _);
                _history.TryRemove(policyId, out _);
                return await Task.FromResult(removed);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> SaveVersionAsync(PolicyVersion version, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var cloned = CloneVersion(version);
                var policyVersions = _versions.GetOrAdd(version.PolicyId, _ => new ConcurrentDictionary<string, PolicyVersion>());
                policyVersions[version.VersionTag] = cloned;
                return await Task.FromResult(true);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PolicyVersion>> GetVersionsAsync(string policyId, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (_versions.TryGetValue(policyId, out var policyVersions))
                {
                    var list = policyVersions.Values.Select(CloneVersion).ToList();
                    return await Task.FromResult<IReadOnlyList<PolicyVersion>>(list);
                }
                return await Task.FromResult<IReadOnlyList<PolicyVersion>>(Array.Empty<PolicyVersion>());
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<PolicyVersion?> GetVersionAsync(string policyId, string versionTag, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (_versions.TryGetValue(policyId, out var policyVersions))
                {
                    if (policyVersions.TryGetValue(versionTag, out var version))
                    {
                        return await Task.FromResult(CloneVersion(version));
                    }
                }
                return await Task.FromResult<PolicyVersion?>(null);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> SaveAssignmentAsync(DomainPolicyAssignment assignment, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                _assignments[assignment.AssignmentId] = assignment with { };
                return await Task.FromResult(true);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<DomainPolicyAssignment?> GetAssignmentAsync(string assignmentId, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (_assignments.TryGetValue(assignmentId, out var assignment))
                {
                    return await Task.FromResult(assignment with { });
                }
                return await Task.FromResult<DomainPolicyAssignment?>(null);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DomainPolicyAssignment>> GetAssignmentsForTargetAsync(string targetId, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var list = _assignments.Values
                    .Where(a => a.Target.TargetId == targetId)
                    .Select(a => a with { })
                    .ToList();
                return await Task.FromResult<IReadOnlyList<DomainPolicyAssignment>>(list);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DomainPolicyAssignment>> GetAllAssignmentsAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var list = _assignments.Values.Select(a => a with { }).ToList();
                return await Task.FromResult<IReadOnlyList<DomainPolicyAssignment>>(list);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAssignmentAsync(string assignmentId, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                bool removed = _assignments.TryRemove(assignmentId, out _);
                return await Task.FromResult(removed);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> SaveHistoryAsync(PolicyHistory history, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var list = _history.GetOrAdd(history.PolicyId, _ => new List<PolicyHistory>());
                list.Add(history with { });
                return await Task.FromResult(true);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PolicyHistory>> GetHistoryAsync(string policyId, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (_history.TryGetValue(policyId, out var list))
                {
                    var result = list.Select(h => h with { }).ToList();
                    return await Task.FromResult<IReadOnlyList<PolicyHistory>>(result);
                }
                return await Task.FromResult<IReadOnlyList<PolicyHistory>>(Array.Empty<PolicyHistory>());
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> SaveComplianceRecordAsync(PolicyComplianceRecord record, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var cloned = CloneCompliance(record);
                _compliance[record.MachineId] = cloned;
                return await Task.FromResult(true);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<PolicyComplianceRecord?> GetComplianceRecordAsync(string machineId, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (_compliance.TryGetValue(machineId, out var record))
                {
                    return await Task.FromResult(CloneCompliance(record));
                }
                return await Task.FromResult<PolicyComplianceRecord?>(null);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PolicyComplianceRecord>> GetAllComplianceRecordsAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var list = _compliance.Values.Select(CloneCompliance).ToList();
                return await Task.FromResult<IReadOnlyList<PolicyComplianceRecord>>(list);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
