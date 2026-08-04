using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Domain.Policy;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Policy.Interfaces
{
    using DomainPolicyAssignment = Sayra.Client.Shared.Models.Phase9.Domain.Policy.PolicyAssignment;
    using FleetMachineInfo = Sayra.Client.Shared.Models.Phase9.Domain.MachineInfo;

    /// <summary>
    /// Thread-safe storage repository for policies, versions, assignments, history, and compliance states.
    /// </summary>
    public interface IPolicyRepository
    {
        // Policy Storage
        Task<bool> SavePolicyAsync(PolicyDefinition policy, CancellationToken ct = default);
        Task<PolicyDefinition?> GetPolicyAsync(string policyId, CancellationToken ct = default);
        Task<IReadOnlyList<PolicyDefinition>> GetAllPoliciesAsync(CancellationToken ct = default);
        Task<bool> DeletePolicyAsync(string policyId, CancellationToken ct = default);

        // Version Storage
        Task<bool> SaveVersionAsync(PolicyVersion version, CancellationToken ct = default);
        Task<IReadOnlyList<PolicyVersion>> GetVersionsAsync(string policyId, CancellationToken ct = default);
        Task<PolicyVersion?> GetVersionAsync(string policyId, string versionTag, CancellationToken ct = default);

        // Assignment Storage
        Task<bool> SaveAssignmentAsync(DomainPolicyAssignment assignment, CancellationToken ct = default);
        Task<DomainPolicyAssignment?> GetAssignmentAsync(string assignmentId, CancellationToken ct = default);
        Task<IReadOnlyList<DomainPolicyAssignment>> GetAssignmentsForTargetAsync(string targetId, CancellationToken ct = default);
        Task<IReadOnlyList<DomainPolicyAssignment>> GetAllAssignmentsAsync(CancellationToken ct = default);
        Task<bool> DeleteAssignmentAsync(string assignmentId, CancellationToken ct = default);

        // History Storage
        Task<bool> SaveHistoryAsync(PolicyHistory history, CancellationToken ct = default);
        Task<IReadOnlyList<PolicyHistory>> GetHistoryAsync(string policyId, CancellationToken ct = default);

        // Compliance Storage
        Task<bool> SaveComplianceRecordAsync(PolicyComplianceRecord record, CancellationToken ct = default);
        Task<PolicyComplianceRecord?> GetComplianceRecordAsync(string machineId, CancellationToken ct = default);
        Task<IReadOnlyList<PolicyComplianceRecord>> GetAllComplianceRecordsAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Memory cache for high-frequency access to active policies and compliance metrics.
    /// </summary>
    public interface IPolicyCache
    {
        Task<PolicyDefinition?> GetPolicyAsync(string policyId, Func<Task<PolicyDefinition?>> factory, TimeSpan? expiration = null);
        Task<PolicyComplianceRecord?> GetComplianceRecordAsync(string machineId, Func<Task<PolicyComplianceRecord?>> factory, TimeSpan? expiration = null);
        Task InvalidatePolicyAsync(string policyId);
        Task InvalidateComplianceRecordAsync(string machineId);
        Task ClearAllAsync();
    }

    /// <summary>
    /// Service governing policy template version tags, semantic versioning validation, and change tracking.
    /// </summary>
    public interface IPolicyVersionManager
    {
        Task<PolicyVersion> CreateVersionAsync(string policyId, List<PolicyRule> rules, string changeSummary, string operatorId, string? versionTag = null, CancellationToken ct = default);
        Task<bool> ValidateSemVerAsync(string versionTag, CancellationToken ct = default);
        Task<int> CompareVersionsAsync(string v1, string v2, CancellationToken ct = default);
        Task<IReadOnlyList<PolicyVersion>> GetVersionHistoryAsync(string policyId, CancellationToken ct = default);
    }

    /// <summary>
    /// Service for resolving inheritance trees and merging policy properties based on priority levels.
    /// </summary>
    public interface IPolicyAssignmentManager
    {
        Task<DomainPolicyAssignment> CreateAssignmentAsync(string policyId, string versionTag, PolicyTarget target, int priority, bool isOverrideEnabled, DateTime? expiresAtUtc, string operatorId, CancellationToken ct = default);
        Task<bool> RemoveAssignmentAsync(string assignmentId, CancellationToken ct = default);
        Task<IReadOnlyList<DomainPolicyAssignment>> GetActiveAssignmentsForMachineAsync(FleetMachineInfo machine, CancellationToken ct = default);
        Task<PolicyDefinition> ResolveAndMergePoliciesAsync(FleetMachineInfo machine, CancellationToken ct = default);
    }

    /// <summary>
    /// Service for comprehensive structural, schema, range, allowed values, dependency, and cryptographic signature verification.
    /// </summary>
    public interface IPolicyValidator
    {
        Task<bool> ValidateSchemaAsync(PolicyDefinition policy, CancellationToken ct = default);
        Task<bool> ValidateDependenciesAsync(PolicyDefinition policy, CancellationToken ct = default);
        Task<bool> ValidateCompatibilityAsync(PolicyDefinition policy, FleetMachineInfo machine, CancellationToken ct = default);
        Task<bool> VerifySignatureAsync(PolicyVersion version, CancellationToken ct = default);
    }

    /// <summary>
    /// Engine representing difference results between two versioned policy structures.
    /// </summary>
    public interface IPolicyDiffEngine
    {
        Task<PolicyComparisonReport> CompareVersionsAsync(PolicyVersion oldVersion, PolicyVersion newVersion, CancellationToken ct = default);
    }

    /// <summary>
    /// Analysis engine generating potential regressions or affected counts prior to policy activation.
    /// </summary>
    public interface IPolicyPreviewEngine
    {
        Task<PolicyPreviewReport> GeneratePreviewAsync(string policyId, string versionTag, PolicyTarget target, CancellationToken ct = default);
    }

    /// <summary>
    /// Service evaluating overall compliance metrics and recording constraint violations.
    /// </summary>
    public interface IComplianceEngine
    {
        Task<PolicyComplianceRecord> EvaluateComplianceAsync(string machineId, Dictionary<string, string> actualSystemState, CancellationToken ct = default);
        Task<IReadOnlyList<PolicyViolation>> GetViolationsAsync(string machineId, CancellationToken ct = default);
    }

    /// <summary>
    /// Manager reverting policy assignments to pre-existing active states.
    /// </summary>
    public interface IRollbackManager
    {
        Task<PolicyVersion> RollbackToVersionAsync(string policyId, string versionTag, string operatorId, CancellationToken ct = default);
        Task<IReadOnlyList<PolicyHistory>> GetRollbackHistoryAsync(string policyId, CancellationToken ct = default);
    }

    /// <summary>
    /// Internal coordination engine of the policy administration lifecycles.
    /// </summary>
    public interface IPolicyAdministrationEngine
    {
        Task<PolicyDefinition> CreatePolicyAsync(string name, string description, string category, string operatorId, CancellationToken ct = default);
        Task<PolicyDefinition> UpdatePolicyAsync(string policyId, string name, string description, string category, string operatorId, CancellationToken ct = default);
        Task<bool> ArchivePolicyAsync(string policyId, string operatorId, CancellationToken ct = default);
        Task<PolicyVersion> PublishVersionAsync(string policyId, string versionTag, List<PolicyRule> rules, string changeSummary, string operatorId, CancellationToken ct = default);
        Task<PolicyDefinition> ClonePolicyAsync(string sourcePolicyId, string newName, string operatorId, CancellationToken ct = default);
    }

    /// <summary>
    /// Main orchestrator service integrating all policy admin subcomponents.
    /// </summary>
    public interface IPolicyManager
    {
        IPolicyRepository Repository { get; }
        IPolicyCache Cache { get; }
        IPolicyVersionManager Versions { get; }
        IPolicyAssignmentManager Assignments { get; }
        IPolicyValidator Validator { get; }
        IPolicyDiffEngine DiffEngine { get; }
        IPolicyPreviewEngine PreviewEngine { get; }
        IComplianceEngine Compliance { get; }
        IRollbackManager Rollback { get; }
        IPolicyAdministrationEngine Administration { get; }
    }

    #region Report Models

    /// <summary>
    /// Detailed report representing the rule and value modifications between two policy versions.
    /// </summary>
    public record PolicyComparisonReport
    {
        public string PolicyId { get; init; } = string.Empty;
        public string OldVersionTag { get; init; } = string.Empty;
        public string NewVersionTag { get; init; } = string.Empty;
        public List<PolicyRule> AddedRules { get; init; } = new();
        public List<PolicyRule> RemovedRules { get; init; } = new();
        public List<string> ChangedValues { get; init; } = new();
        public bool IsDifferent => AddedRules.Count > 0 || RemovedRules.Count > 0 || ChangedValues.Count > 0;
    }

    /// <summary>
    /// Predictive evaluation report showing the scope, count of affected machines, and potential conflicts.
    /// </summary>
    public record PolicyPreviewReport
    {
        public string PolicyId { get; init; } = string.Empty;
        public string VersionTag { get; init; } = string.Empty;
        public PolicyTarget Target { get; init; } = new();
        public int AffectedWorkstationsCount { get; init; }
        public List<string> ChangeSummary { get; init; } = new();
        public List<string> PotentialConflicts { get; init; } = new();
        public string ImpactSeverity { get; init; } = "Low";
    }

    #endregion
}
