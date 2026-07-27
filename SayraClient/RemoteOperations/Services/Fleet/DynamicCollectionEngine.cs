using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services.Fleet
{
    public class DynamicCollectionEngine
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly ILogger<DynamicCollectionEngine> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public DynamicCollectionEngine(ILocalDatabaseService databaseService, ILogger<DynamicCollectionEngine> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CreateCollectionAsync(DynamicCollection collection, CancellationToken cancellationToken = default)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO DynamicCollections (CollectionId, Name, RuleJson, CreatedAt)
                VALUES ($id, $name, $rule, $createdAt);";

            cmd.Parameters.Add(CreateParam(cmd, "$id", collection.CollectionId));
            cmd.Parameters.Add(CreateParam(cmd, "$name", collection.Name));
            cmd.Parameters.Add(CreateParam(cmd, "$rule", collection.RuleJson));
            cmd.Parameters.Add(CreateParam(cmd, "$createdAt", collection.CreatedAt.ToString("O")));

            await cmd.ExecuteNonQueryAsync(cancellationToken);

            await EvaluateAllMembershipsAsync(cancellationToken);
        }

        public async Task DeleteCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM DynamicCollections WHERE CollectionId = $id;";
                    cmd.Parameters.Add(CreateParam(cmd, "$id", collectionId));
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM CollectionMembership WHERE CollectionId = $id;";
                    cmd.Parameters.Add(CreateParam(cmd, "$id", collectionId));
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to delete collection '{CollectionId}'", collectionId);
                throw;
            }
        }

        public async Task<List<string>> GetCollectionMachinesAsync(string collectionId, CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MachineId FROM CollectionMembership WHERE CollectionId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", collectionId));

            var list = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(reader.GetString(0));
            }
            return list;
        }

        public async Task EvaluateAllMembershipsAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Re-evaluating dynamic collections memberships...");

                using var connection = _databaseService.CreateConnection();
                await connection.OpenAsync(cancellationToken);

                var collections = new List<DynamicCollection>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT CollectionId, Name, RuleJson, CreatedAt FROM DynamicCollections;";
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        collections.Add(new DynamicCollection
                        {
                            CollectionId = reader.GetString(0),
                            Name = reader.GetString(1),
                            RuleJson = reader.GetString(2),
                            CreatedAt = DateTime.Parse(reader.GetString(3))
                        });
                    }
                }

                var workstations = new List<(string MachineId, Dictionary<string, string> Metadata)>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT MachineId, MetadataJson FROM Workstations;";
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var mId = reader.GetString(0);
                        var metaJson = reader.GetString(1);
                        var meta = new Dictionary<string, string>();
                        try
                        {
                            meta = JsonSerializer.Deserialize<Dictionary<string, string>>(metaJson) ?? new();
                        }
                        catch { }
                        workstations.Add((mId, meta));
                    }
                }

                using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM CollectionMembership;";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    foreach (var col in collections)
                    {
                        CollectionRule? rule = null;
                        try
                        {
                            rule = JsonSerializer.Deserialize<CollectionRule>(col.RuleJson);
                        }
                        catch { }

                        if (rule == null) continue;

                        foreach (var ws in workstations)
                        {
                            if (EvaluateRule(rule, ws.Metadata))
                            {
                                using var cmd = connection.CreateCommand();
                                cmd.Transaction = transaction;
                                cmd.CommandText = @"
                                    INSERT INTO CollectionMembership (MembershipId, MachineId, CollectionId, JoinedAt)
                                    VALUES ($id, $machineId, $collId, $joinedAt);";

                                cmd.Parameters.Add(CreateParam(cmd, "$id", Guid.NewGuid().ToString()));
                                cmd.Parameters.Add(CreateParam(cmd, "$machineId", ws.MachineId));
                                cmd.Parameters.Add(CreateParam(cmd, "$collId", col.CollectionId));
                                cmd.Parameters.Add(CreateParam(cmd, "$joinedAt", DateTime.UtcNow.ToString("O")));

                                await cmd.ExecuteNonQueryAsync(cancellationToken);
                            }
                        }
                    }

                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Failed to apply dynamic collection membership evaluation.");
                    throw;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private bool EvaluateRule(CollectionRule rule, Dictionary<string, string> metadata)
        {
            if (string.IsNullOrEmpty(rule.Metric) || !metadata.TryGetValue(rule.Metric, out var value))
            {
                return false;
            }

            switch (rule.Operator)
            {
                case "==":
                    return string.Equals(value, rule.Value, StringComparison.OrdinalIgnoreCase);
                case "!=":
                    return !string.Equals(value, rule.Value, StringComparison.OrdinalIgnoreCase);
                case ">":
                    if (double.TryParse(value, out double v1) && double.TryParse(rule.Value, out double r1))
                        return v1 > r1;
                    return string.Compare(value, rule.Value, StringComparison.OrdinalIgnoreCase) > 0;
                case ">=":
                    if (double.TryParse(value, out double v2) && double.TryParse(rule.Value, out double r2))
                        return v2 >= r2;
                    return string.Compare(value, rule.Value, StringComparison.OrdinalIgnoreCase) >= 0;
                case "<":
                    if (double.TryParse(value, out double v3) && double.TryParse(rule.Value, out double r3))
                        return v3 < r3;
                    return string.Compare(value, rule.Value, StringComparison.OrdinalIgnoreCase) < 0;
                case "<=":
                    if (double.TryParse(value, out double v4) && double.TryParse(rule.Value, out double r4))
                        return v4 <= r4;
                    return string.Compare(value, rule.Value, StringComparison.OrdinalIgnoreCase) <= 0;
                default:
                    return false;
            }
        }

        private static DbParameter CreateParam(DbCommand cmd, string name, object? value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            return param;
        }

        public class CollectionRule
        {
            public string Metric { get; set; } = string.Empty;
            public string Operator { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }
    }
}
