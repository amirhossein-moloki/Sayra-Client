using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Models.Phase9.Domain.Policy
{
    /// <summary>
    /// Utility helper for comparing collection properties within immutable records to ensure value-based equality.
    /// </summary>
    public static class PolicyEqualityUtility
    {
        /// <summary>
        /// Compares two lists of elements for value equality.
        /// </summary>
        public static bool ListEquals<T>(List<T>? first, List<T>? second)
        {
            if (ReferenceEquals(first, second)) return true;
            if (first is null || second is null) return false;
            if (first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(first[i], second[i])) return false;
            }
            return true;
        }

        /// <summary>
        /// Compares two dictionaries of elements for value equality.
        /// </summary>
        public static bool DictionaryEquals<TKey, TValue>(Dictionary<TKey, TValue>? first, Dictionary<TKey, TValue>? second) where TKey : notnull
        {
            if (ReferenceEquals(first, second)) return true;
            if (first is null || second is null) return false;
            if (first.Count != second.Count) return false;
            foreach (var kvp in first)
            {
                if (!second.TryGetValue(kvp.Key, out var otherVal) || !EqualityComparer<TValue>.Default.Equals(kvp.Value, otherVal)) return false;
            }
            return true;
        }

        /// <summary>
        /// Generates a hash code combining list elements.
        /// </summary>
        public static int GetListHashCode<T>(List<T>? list)
        {
            if (list is null) return 0;
            var hash = new HashCode();
            foreach (var item in list)
            {
                hash.Add(item);
            }
            return hash.ToHashCode();
        }

        /// <summary>
        /// Generates a hash code combining dictionary elements.
        /// </summary>
        public static int GetDictionaryHashCode<TKey, TValue>(Dictionary<TKey, TValue>? dict) where TKey : notnull
        {
            if (dict is null) return 0;
            var hash = new HashCode();
            foreach (var kvp in dict)
            {
                hash.Add(kvp.Key);
                hash.Add(kvp.Value);
            }
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Value Object representing a dynamic execution parameter argument.
    /// </summary>
    public record PolicyParameter
    {
        /// <summary>
        /// Gets the parameter name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the parameter serialized value.
        /// </summary>
        public string Value { get; init; } = string.Empty;

        /// <summary>
        /// Gets the type category of parameter (e.g. String, Int, Bool).
        /// </summary>
        public string Type { get; init; } = "String";
    }

    /// <summary>
    /// Value Object representing evaluation conditions for the policy rule.
    /// </summary>
    public record PolicyCondition
    {
        /// <summary>
        /// Gets the target evaluation field name.
        /// </summary>
        public string Field { get; init; } = string.Empty;

        /// <summary>
        /// Gets the condition comparison operator (e.g. Equals, GreaterThan, Contains).
        /// </summary>
        public string Operator { get; init; } = "Equals";

        /// <summary>
        /// Gets the value compared against.
        /// </summary>
        public string Value { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents a single modular policy rule.
    /// </summary>
    public record PolicyRule
    {
        /// <summary>
        /// Gets the unique identifier for the policy rule.
        /// </summary>
        public string RuleId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the descriptive name of the rule.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the rule category.
        /// </summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>
        /// Gets the rule parameters.
        /// </summary>
        public List<PolicyParameter> Parameters { get; init; } = new();

        /// <summary>
        /// Gets the rule conditions.
        /// </summary>
        public List<PolicyCondition> Conditions { get; init; } = new();

        /// <summary>
        /// Determines value equality for all properties including deep list comparison.
        /// </summary>
        public virtual bool Equals(PolicyRule? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return RuleId == other.RuleId &&
                   Name == other.Name &&
                   Category == other.Category &&
                   PolicyEqualityUtility.ListEquals(Parameters, other.Parameters) &&
                   PolicyEqualityUtility.ListEquals(Conditions, other.Conditions);
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(RuleId);
            hash.Add(Name);
            hash.Add(Category);
            hash.Add(PolicyEqualityUtility.GetListHashCode(Parameters));
            hash.Add(PolicyEqualityUtility.GetListHashCode(Conditions));
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Represents the custom organization and author metadata for a policy definition.
    /// </summary>
    public record PolicyMetadata
    {
        /// <summary>
        /// Gets the author of the policy template.
        /// </summary>
        public string Author { get; init; } = string.Empty;

        /// <summary>
        /// Gets the organization owning this policy.
        /// </summary>
        public string Organization { get; init; } = string.Empty;

        /// <summary>
        /// Gets the target environment (e.g. Production, Staging, Testing).
        /// </summary>
        public string Environment { get; init; } = "Production";

        /// <summary>
        /// Gets dictionary of other custom or extensible metadata attributes.
        /// </summary>
        public Dictionary<string, string> CustomAttributes { get; init; } = new();

        /// <summary>
        /// Determines value equality for all properties including deep dictionary comparison.
        /// </summary>
        public virtual bool Equals(PolicyMetadata? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Author == other.Author &&
                   Organization == other.Organization &&
                   Environment == other.Environment &&
                   PolicyEqualityUtility.DictionaryEquals(CustomAttributes, other.CustomAttributes);
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Author);
            hash.Add(Organization);
            hash.Add(Environment);
            hash.Add(PolicyEqualityUtility.GetDictionaryHashCode(CustomAttributes));
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Represents a central security, configuration, or operational policy template.
    /// </summary>
    public record PolicyDefinition
    {
        /// <summary>
        /// Gets the unique template identifier.
        /// </summary>
        public string PolicyId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the policy name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the detailed policy description.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets the policy type category (e.g., Security, Kiosk, GamingCenter).
        /// </summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>
        /// Gets the active semantic version tag.
        /// </summary>
        public string ActiveVersionTag { get; init; } = "1.0.0";

        /// <summary>
        /// Gets the list of policy rules.
        /// </summary>
        public List<PolicyRule> Rules { get; init; } = new();

        /// <summary>
        /// Gets the template metadata.
        /// </summary>
        public PolicyMetadata Metadata { get; init; } = new();

        /// <summary>
        /// Gets whether the policy has been archived.
        /// </summary>
        public bool IsArchived { get; init; }

        /// <summary>
        /// Gets the policy creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the operator identifier of the creator.
        /// </summary>
        public string CreatedBy { get; init; } = string.Empty;

        /// <summary>
        /// Determines value equality for all properties.
        /// </summary>
        public virtual bool Equals(PolicyDefinition? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return PolicyId == other.PolicyId &&
                   Name == other.Name &&
                   Description == other.Description &&
                   Category == other.Category &&
                   ActiveVersionTag == other.ActiveVersionTag &&
                   IsArchived == other.IsArchived &&
                   CreatedBy == other.CreatedBy &&
                   PolicyEqualityUtility.ListEquals(Rules, other.Rules) &&
                   Metadata.Equals(other.Metadata);
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(PolicyId);
            hash.Add(Name);
            hash.Add(Description);
            hash.Add(Category);
            hash.Add(ActiveVersionTag);
            hash.Add(IsArchived);
            hash.Add(CreatedBy);
            hash.Add(PolicyEqualityUtility.GetListHashCode(Rules));
            hash.Add(Metadata);
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Represents a specific immutable version of a policy definition.
    /// </summary>
    public record PolicyVersion
    {
        /// <summary>
        /// Gets the associated policy definition identifier.
        /// </summary>
        public string PolicyId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the semantic version tag (e.g. 1.0.0).
        /// </summary>
        public string VersionTag { get; init; } = "1.0.0";

        /// <summary>
        /// Gets the summary changes introduced in this version.
        /// </summary>
        public string ChangeSummary { get; init; } = string.Empty;

        /// <summary>
        /// Gets the frozen policy rules assigned to this version.
        /// </summary>
        public List<PolicyRule> Rules { get; init; } = new();

        /// <summary>
        /// Gets the SHA-256 digital signature of this version's rules and structure.
        /// </summary>
        public string ContentHash { get; init; } = string.Empty;

        /// <summary>
        /// Gets the cryptographically chained digital signature verifying authenticity.
        /// </summary>
        public string Signature { get; init; } = string.Empty;

        /// <summary>
        /// Gets the version creation timestamp.
        /// </summary>
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the operator identifier of the creator.
        /// </summary>
        public string CreatedBy { get; init; } = string.Empty;

        /// <summary>
        /// Determines value equality for all properties.
        /// </summary>
        public virtual bool Equals(PolicyVersion? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return PolicyId == other.PolicyId &&
                   VersionTag == other.VersionTag &&
                   ChangeSummary == other.ChangeSummary &&
                   ContentHash == other.ContentHash &&
                   Signature == other.Signature &&
                   CreatedBy == other.CreatedBy &&
                   PolicyEqualityUtility.ListEquals(Rules, other.Rules);
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(PolicyId);
            hash.Add(VersionTag);
            hash.Add(ChangeSummary);
            hash.Add(ContentHash);
            hash.Add(Signature);
            hash.Add(CreatedBy);
            hash.Add(PolicyEqualityUtility.GetListHashCode(Rules));
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Value Object representing target identifiers and target types.
    /// </summary>
    public record PolicyTarget
    {
        /// <summary>
        /// Gets the target identifier (e.g., specific Machine ID, Group ID, Region Name, Tag Key).
        /// </summary>
        public string TargetId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the targeting division classification (e.g., Machine, Group, Region, Department, Tag, DynamicGroup).
        /// </summary>
        public string TargetType { get; init; } = "Machine";
    }

    /// <summary>
    /// Value Object representing assignment scope, inheritance, and sources.
    /// </summary>
    public record PolicyScope
    {
        /// <summary>
        /// Gets the scope identifier.
        /// </summary>
        public string ScopeId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the target properties.
        /// </summary>
        public PolicyTarget Target { get; init; } = new();

        /// <summary>
        /// Gets whether the scope is inherited.
        /// </summary>
        public bool IsInherited { get; init; }

        /// <summary>
        /// Gets the original target source from which the scope was inherited.
        /// </summary>
        public string? SourceTargetId { get; init; }
    }

    /// <summary>
    /// Represents the assignment application mapping linking target scopes to versioned policies.
    /// </summary>
    public record PolicyAssignment
    {
        /// <summary>
        /// Gets the assignment identifier.
        /// </summary>
        public string AssignmentId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the associated policy definition identifier.
        /// </summary>
        public string PolicyId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the specific version tag assigned.
        /// </summary>
        public string VersionTag { get; init; } = "1.0.0";

        /// <summary>
        /// Gets the targeted scope details.
        /// </summary>
        public PolicyTarget Target { get; init; } = new();

        /// <summary>
        /// Gets the application priority.
        /// </summary>
        public int Priority { get; init; } = 100;

        /// <summary>
        /// Gets whether override rules are enabled.
        /// </summary>
        public bool IsOverrideEnabled { get; init; } = true;

        /// <summary>
        /// Gets the expiration timestamp, if any.
        /// </summary>
        public DateTime? ExpiresAtUtc { get; init; }

        /// <summary>
        /// Gets the active assignment compliance status.
        /// </summary>
        public PolicyAssignmentStatus Status { get; init; } = PolicyAssignmentStatus.Pending;

        /// <summary>
        /// Gets details about any policy application failures.
        /// </summary>
        public string FailureReason { get; init; } = string.Empty;

        /// <summary>
        /// Gets when the policy assignment was initialized.
        /// </summary>
        public DateTime AssignedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the operator identifier of the creator.
        /// </summary>
        public string AssignedBy { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents the audit action or state transition of a policy definition.
    /// </summary>
    public record PolicyHistory
    {
        /// <summary>
        /// Gets unique history tracking identifier.
        /// </summary>
        public string HistoryId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the policy definition identifier.
        /// </summary>
        public string PolicyId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the version tag related to history event.
        /// </summary>
        public string VersionTag { get; init; } = "1.0.0";

        /// <summary>
        /// Gets the lifecycle action verb (e.g. Create, Update, Publish, Rollback).
        /// </summary>
        public string Action { get; init; } = "Create";

        /// <summary>
        /// Gets when history event was saved.
        /// </summary>
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the operator identifier who executed the action.
        /// </summary>
        public string Actor { get; init; } = string.Empty;

        /// <summary>
        /// Gets additional structured log details.
        /// </summary>
        public string Details { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents a specific policy violation detected during compliance evaluations.
    /// </summary>
    public record PolicyViolation
    {
        /// <summary>
        /// Gets the unique violation tracking identifier.
        /// </summary>
        public string ViolationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the violating policy definition identifier.
        /// </summary>
        public string PolicyId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the violating rule identifier.
        /// </summary>
        public string RuleId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the non-compliant client machine identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets when the violation was detected.
        /// </summary>
        public DateTime DetectedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets descriptive log message of violation details.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets severity level of violation (e.g. Warning, Critical).
        /// </summary>
        public string Severity { get; init; } = "Warning";
    }

    /// <summary>
    /// Represents overall compliance evaluation records generated for workstations.
    /// </summary>
    public record PolicyComplianceRecord
    {
        /// <summary>
        /// Gets unique record tracking identifier.
        /// </summary>
        public string RecordId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the evaluated client machine identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets when the evaluation took place.
        /// </summary>
        public DateTime EvaluationTimeUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets overall compliance status.
        /// </summary>
        public ComplianceStatus OverallStatus { get; init; } = ComplianceStatus.Evaluating;

        /// <summary>
        /// Gets evaluated mathematical score (0.0 to 100.0).
        /// </summary>
        public double ComplianceScore { get; init; } = 100.0;

        /// <summary>
        /// Gets the total number of assigned policies evaluated.
        /// </summary>
        public int CheckedPoliciesCount { get; init; }

        /// <summary>
        /// Gets the total number of rules that violated constraints.
        /// </summary>
        public int ViolationsCount => Violations.Count;

        /// <summary>
        /// Gets list of identified policy violations.
        /// </summary>
        public List<PolicyViolation> Violations { get; init; } = new();

        /// <summary>
        /// Determines value equality for all properties including deep list comparison.
        /// </summary>
        public virtual bool Equals(PolicyComplianceRecord? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return RecordId == other.RecordId &&
                   MachineId == other.MachineId &&
                   OverallStatus == other.OverallStatus &&
                   Math.Abs(ComplianceScore - other.ComplianceScore) < 0.0001 &&
                   CheckedPoliciesCount == other.CheckedPoliciesCount &&
                   PolicyEqualityUtility.ListEquals(Violations, other.Violations);
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(RecordId);
            hash.Add(MachineId);
            hash.Add(OverallStatus);
            hash.Add(ComplianceScore);
            hash.Add(CheckedPoliciesCount);
            hash.Add(PolicyEqualityUtility.GetListHashCode(Violations));
            return hash.ToHashCode();
        }
    }
}
