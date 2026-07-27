using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class PolicyRepository : IPolicyRepository
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly ILogger<PolicyRepository> _logger;

        public PolicyRepository(ILocalDatabaseService databaseService, ILogger<PolicyRepository> _loggerVal)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _logger = _loggerVal ?? throw new ArgumentNullException(nameof(_loggerVal));
        }

        public async Task SavePolicyAsync(PolicyProfile profile, CancellationToken cancellationToken = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            _logger.LogInformation("Saving policy profile '{PolicyId}' (Version: {Version}) to secure storage...", profile.PolicyId, profile.Version);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO AppliedPolicies (PolicyId, Category, RulesJson, VersionCode, LastUpdatedAt, IsActive, Signature)
                    VALUES ($policyId, $category, $rulesJson, $versionCode, $lastUpdatedAt, $isActive, $signature)
                    ON CONFLICT(PolicyId) DO UPDATE SET
                        Category = excluded.Category,
                        RulesJson = excluded.RulesJson,
                        VersionCode = excluded.VersionCode,
                        LastUpdatedAt = excluded.LastUpdatedAt,
                        IsActive = excluded.IsActive,
                        Signature = excluded.Signature;";

                cmd.Parameters.Add(CreateParam(cmd, "$policyId", profile.PolicyId));

                string category = "WINDOWS";
                if (profile.Rules != null && profile.Rules.Count > 0)
                {
                    category = profile.Rules[0].Category.ToString();
                }
                cmd.Parameters.Add(CreateParam(cmd, "$category", category));

                string rulesJson = JsonSerializer.Serialize(profile.Rules);
                cmd.Parameters.Add(CreateParam(cmd, "$rulesJson", rulesJson));
                cmd.Parameters.Add(CreateParam(cmd, "$versionCode", profile.Version));
                cmd.Parameters.Add(CreateParam(cmd, "$lastUpdatedAt", DateTime.UtcNow.ToString("O")));
                cmd.Parameters.Add(CreateParam(cmd, "$isActive", 1));
                cmd.Parameters.Add(CreateParam(cmd, "$signature", profile.Signature));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to save policy profile '{PolicyId}' inside secure database.", profile.PolicyId);
                throw;
            }
        }

        public async Task<List<PolicyProfile>> LoadPoliciesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Loading all policy profiles from secure database...");

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT PolicyId, Category, RulesJson, VersionCode, LastUpdatedAt, IsActive, Signature
                FROM AppliedPolicies;";

            var list = new List<PolicyProfile>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                string rulesJson = reader.GetString(2);
                var rules = JsonSerializer.Deserialize<List<PolicyRule>>(rulesJson) ?? new List<PolicyRule>();

                list.Add(new PolicyProfile
                {
                    PolicyId = reader.GetString(0),
                    Version = reader.GetInt64(3),
                    Rules = rules,
                    Signature = reader.GetString(6),
                    IssuedAt = DateTime.Parse(reader.GetString(4))
                });
            }

            return list;
        }

        public async Task<long> GetPolicyVersionAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MAX(VersionCode) FROM AppliedPolicies WHERE IsActive = 1;";
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result != null && result != DBNull.Value)
            {
                return Convert.ToInt64(result);
            }
            return 0;
        }

        public async Task DeletePolicyAsync(string policyId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(policyId)) throw new ArgumentNullException(nameof(policyId));

            _logger.LogInformation("Deleting policy profile '{PolicyId}' from secure storage...", policyId);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM AppliedPolicies WHERE PolicyId = $policyId;";
            cmd.Parameters.Add(CreateParam(cmd, "$policyId", policyId));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<List<PolicyProfile>> GetActivePoliciesAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT PolicyId, Category, RulesJson, VersionCode, LastUpdatedAt, IsActive, Signature
                FROM AppliedPolicies
                WHERE IsActive = 1;";

            var list = new List<PolicyProfile>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                string rulesJson = reader.GetString(2);
                var rules = JsonSerializer.Deserialize<List<PolicyRule>>(rulesJson) ?? new List<PolicyRule>();

                list.Add(new PolicyProfile
                {
                    PolicyId = reader.GetString(0),
                    Version = reader.GetInt64(3),
                    Rules = rules,
                    Signature = reader.GetString(6),
                    IssuedAt = DateTime.Parse(reader.GetString(4))
                });
            }

            return list;
        }

        private static DbParameter CreateParam(DbCommand cmd, string name, object? value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            return param;
        }
    }
}
