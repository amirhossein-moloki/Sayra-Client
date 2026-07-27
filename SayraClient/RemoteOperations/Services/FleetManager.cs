using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class FleetManager : IFleetManager
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly IGroupRepository _groupRepository;
        private readonly ILogger<FleetManager> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public FleetManager(
            ILocalDatabaseService databaseService,
            IGroupRepository groupRepository,
            ILogger<FleetManager> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region IFleetManager Implementation

        public async Task RegisterWorkstationAsync(Workstation workstation, CancellationToken ct = default)
        {
            if (workstation == null) throw new ArgumentNullException(nameof(workstation));

            _logger.LogInformation("Registering workstation '{WorkstationId}' ({Name})", workstation.WorkstationId, workstation.Name);

            await _lock.WaitAsync(ct);
            try
            {
                using var connection = _databaseService.CreateConnection();
                await connection.OpenAsync(ct);

                using var transaction = await connection.BeginTransactionAsync(ct);
                try
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO Workstations (
                            WorkstationId, Name, IpAddress, MacAddress, Status, LastSeen,
                            Version, Gpu, RamGb, WindowsVersion, PolicyVersion, HealthState
                        ) VALUES (
                            $id, $name, $ip, $mac, $status, $lastSeen,
                            $version, $gpu, $ram, $winVer, $policyVer, $health
                        );";

                    cmd.Parameters.Add(CreateParam(cmd, "$id", workstation.WorkstationId));
                    cmd.Parameters.Add(CreateParam(cmd, "$name", workstation.Name));
                    cmd.Parameters.Add(CreateParam(cmd, "$ip", workstation.IpAddress));
                    cmd.Parameters.Add(CreateParam(cmd, "$mac", workstation.MacAddress));
                    cmd.Parameters.Add(CreateParam(cmd, "$status", workstation.Status));
                    cmd.Parameters.Add(CreateParam(cmd, "$lastSeen", workstation.LastSeen));
                    cmd.Parameters.Add(CreateParam(cmd, "$version", workstation.Version));
                    cmd.Parameters.Add(CreateParam(cmd, "$gpu", workstation.Gpu));
                    cmd.Parameters.Add(CreateParam(cmd, "$ram", workstation.RamGb));
                    cmd.Parameters.Add(CreateParam(cmd, "$winVer", workstation.WindowsVersion));
                    cmd.Parameters.Add(CreateParam(cmd, "$policyVer", workstation.PolicyVersion));
                    cmd.Parameters.Add(CreateParam(cmd, "$health", workstation.HealthState));

                    await cmd.ExecuteNonQueryAsync(ct);
                    await transaction.CommitAsync(ct);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogError(ex, "Failed to register workstation '{WorkstationId}'", workstation.WorkstationId);
                    throw;
                }

                await EvaluateWorkstationCollectionsAsync(workstation, ct);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task UpdateMetadataAsync(string workstationId, string ipAddress, string macAddress, string version, string gpu, int ramGb, string winVer, string policyVer, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(workstationId)) throw new ArgumentException("Workstation ID cannot be empty", nameof(workstationId));

            _logger.LogInformation("Updating metadata for workstation '{WorkstationId}'", workstationId);

            await _lock.WaitAsync(ct);
            try
            {
                var ws = await GetWorkstationInternalAsync(workstationId, ct);
                if (ws == null)
                {
                    ws = new Workstation
                    {
                        WorkstationId = workstationId,
                        Name = "Workstation-" + workstationId,
                        Status = "Online"
                    };
                }

                ws.IpAddress = ipAddress;
                ws.MacAddress = macAddress;
                ws.Version = version;
                ws.Gpu = gpu;
                ws.RamGb = ramGb;
                ws.WindowsVersion = winVer;
                ws.PolicyVersion = policyVer;
                ws.LastSeen = DateTime.UtcNow.ToString("O");

                using var connection = _databaseService.CreateConnection();
                await connection.OpenAsync(ct);

                using var transaction = await connection.BeginTransactionAsync(ct);
                try
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        UPDATE Workstations
                        SET IpAddress = $ip, MacAddress = $mac, Version = $version, Gpu = $gpu,
                            RamGb = $ram, WindowsVersion = $winVer, PolicyVersion = $policyVer, LastSeen = $lastSeen
                        WHERE WorkstationId = $id;";

                    cmd.Parameters.Add(CreateParam(cmd, "$ip", ipAddress));
                    cmd.Parameters.Add(CreateParam(cmd, "$mac", macAddress));
                    cmd.Parameters.Add(CreateParam(cmd, "$version", version));
                    cmd.Parameters.Add(CreateParam(cmd, "$gpu", gpu));
                    cmd.Parameters.Add(CreateParam(cmd, "$ram", ramGb));
                    cmd.Parameters.Add(CreateParam(cmd, "$winVer", winVer));
                    cmd.Parameters.Add(CreateParam(cmd, "$policyVer", policyVer));
                    cmd.Parameters.Add(CreateParam(cmd, "$lastSeen", ws.LastSeen));
                    cmd.Parameters.Add(CreateParam(cmd, "$id", workstationId));

                    await cmd.ExecuteNonQueryAsync(ct);
                    await transaction.CommitAsync(ct);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogError(ex, "Failed to update metadata for workstation '{WorkstationId}'", workstationId);
                    throw;
                }

                await EvaluateWorkstationCollectionsAsync(ws, ct);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task AssignToGroupsAsync(string workstationId, List<string> groupIds, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(workstationId)) throw new ArgumentException("Workstation ID cannot be empty", nameof(workstationId));
            if (groupIds == null) throw new ArgumentNullException(nameof(groupIds));

            _logger.LogInformation("Assigning workstation '{WorkstationId}' to {Count} groups", workstationId, groupIds.Count);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                // Delete existing assignments first
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM MachineAssignments WHERE WorkstationId = $id;";
                    cmd.Parameters.Add(CreateParam(cmd, "$id", workstationId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // Add new assignments
                foreach (var groupId in groupIds)
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "INSERT INTO MachineAssignments (WorkstationId, GroupId) VALUES ($wsId, $grpId);";
                        cmd.Parameters.Add(CreateParam(cmd, "$wsId", workstationId));
                        cmd.Parameters.Add(CreateParam(cmd, "$grpId", groupId));
                        await cmd.ExecuteNonQueryAsync(ct);
                    }
                }

                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to assign workstation '{WorkstationId}' to groups.", workstationId);
                throw;
            }
        }

        public async Task RemoveWorkstationAsync(string workstationId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(workstationId)) throw new ArgumentException("Workstation ID cannot be empty", nameof(workstationId));

            _logger.LogInformation("Removing workstation '{WorkstationId}'", workstationId);

            await _lock.WaitAsync(ct);
            try
            {
                using var connection = _databaseService.CreateConnection();
                await connection.OpenAsync(ct);

                using var transaction = await connection.BeginTransactionAsync(ct);
                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM CollectionMembership WHERE WorkstationId = $id;";
                        cmd.Parameters.Add(CreateParam(cmd, "$id", workstationId));
                        await cmd.ExecuteNonQueryAsync(ct);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM MachineAssignments WHERE WorkstationId = $id;";
                        cmd.Parameters.Add(CreateParam(cmd, "$id", workstationId));
                        await cmd.ExecuteNonQueryAsync(ct);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM Workstations WHERE WorkstationId = $id;";
                        cmd.Parameters.Add(CreateParam(cmd, "$id", workstationId));
                        await cmd.ExecuteNonQueryAsync(ct);
                    }

                    await transaction.CommitAsync(ct);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogError(ex, "Failed to remove workstation '{WorkstationId}'", workstationId);
                    throw;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<Workstation?> GetWorkstationAsync(string workstationId, CancellationToken ct = default)
        {
            return await GetWorkstationInternalAsync(workstationId, ct);
        }

        public async Task<List<Workstation>> GetActiveWorkstationsAsync(CancellationToken ct = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT WorkstationId, Name, IpAddress, MacAddress, Status, LastSeen, Version, Gpu, RamGb, WindowsVersion, PolicyVersion, HealthState FROM Workstations;";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<Workstation>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(MapWorkstation(reader));
            }

            return list;
        }

        #endregion

        #region Internal Helper Methods

        private async Task<Workstation?> GetWorkstationInternalAsync(string workstationId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(workstationId)) return null;

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT WorkstationId, Name, IpAddress, MacAddress, Status, LastSeen, Version, Gpu, RamGb, WindowsVersion, PolicyVersion, HealthState FROM Workstations WHERE WorkstationId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", workstationId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return MapWorkstation(reader);
            }

            return null;
        }

        private static Workstation MapWorkstation(DbDataReader reader)
        {
            return new Workstation
            {
                WorkstationId = reader.GetString(0),
                Name = reader.GetString(1),
                IpAddress = reader.GetString(2),
                MacAddress = reader.GetString(3),
                Status = reader.GetString(4),
                LastSeen = reader.GetString(5),
                Version = reader.GetString(6),
                Gpu = reader.GetString(7),
                RamGb = reader.GetInt32(8),
                WindowsVersion = reader.GetString(9),
                PolicyVersion = reader.GetString(10),
                HealthState = reader.GetString(11)
            };
        }

        private static DbParameter CreateParam(DbCommand cmd, string name, object? value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            return param;
        }

        #endregion

        #region DynamicCollectionEngine Implementation

        public async Task CreateDynamicCollectionAsync(DynamicCollection collection, CancellationToken ct = default)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));

            _logger.LogInformation("Creating dynamic collection '{CollectionId}' ({Name}) with expression '{RuleExpression}'", collection.CollectionId, collection.Name, collection.RuleExpression);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO DynamicCollections (CollectionId, Name, RuleExpression, LastUpdatedAt)
                    VALUES ($colId, $name, $expr, $updated);";

                cmd.Parameters.Add(CreateParam(cmd, "$colId", collection.CollectionId));
                cmd.Parameters.Add(CreateParam(cmd, "$name", collection.Name));
                cmd.Parameters.Add(CreateParam(cmd, "$expr", collection.RuleExpression));
                cmd.Parameters.Add(CreateParam(cmd, "$updated", DateTime.UtcNow.ToString("O")));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to create dynamic collection '{CollectionId}'.", collection.CollectionId);
                throw;
            }

            await ReevaluateAllCollectionsAsync(ct);
        }

        public async Task DeleteDynamicCollectionAsync(string collectionId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentException("Collection ID cannot be empty", nameof(collectionId));

            _logger.LogInformation("Deleting dynamic collection '{CollectionId}'", collectionId);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM CollectionMembership WHERE CollectionId = $id;";
                    cmd.Parameters.Add(CreateParam(cmd, "$id", collectionId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM DynamicCollections WHERE CollectionId = $id;";
                    cmd.Parameters.Add(CreateParam(cmd, "$id", collectionId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to delete dynamic collection '{CollectionId}'.", collectionId);
                throw;
            }
        }

        public async Task<List<DynamicCollection>> GetDynamicCollectionsAsync(CancellationToken ct = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT CollectionId, Name, RuleExpression, LastUpdatedAt FROM DynamicCollections;";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<DynamicCollection>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(new DynamicCollection
                {
                    CollectionId = reader.GetString(0),
                    Name = reader.GetString(1),
                    RuleExpression = reader.GetString(2),
                    LastUpdatedAt = reader.GetString(3)
                });
            }

            return list;
        }

        public async Task<List<Workstation>> GetCollectionMembersAsync(string collectionId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentException("Collection ID cannot be empty", nameof(collectionId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT w.WorkstationId, w.Name, w.IpAddress, w.MacAddress, w.Status, w.LastSeen, w.Version, w.Gpu, w.RamGb, w.WindowsVersion, w.PolicyVersion, w.HealthState
                FROM Workstations w
                INNER JOIN CollectionMembership cm ON w.WorkstationId = cm.WorkstationId
                WHERE cm.CollectionId = $colId;";
            cmd.Parameters.Add(CreateParam(cmd, "$colId", collectionId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<Workstation>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(MapWorkstation(reader));
            }

            return list;
        }

        public async Task ReevaluateAllCollectionsAsync(CancellationToken ct = default)
        {
            var workstations = await GetActiveWorkstationsAsync(ct);
            foreach (var ws in workstations)
            {
                await EvaluateWorkstationCollectionsAsync(ws, ct);
            }
        }

        private async Task EvaluateWorkstationCollectionsAsync(Workstation workstation, CancellationToken ct)
        {
            _logger.LogDebug("Evaluating dynamic collection memberships for workstation '{WorkstationId}'", workstation.WorkstationId);

            var collections = await GetDynamicCollectionsAsync(ct);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                foreach (var col in collections)
                {
                    bool isMember = EvaluateRule(workstation, col.RuleExpression);

                    if (isMember)
                    {
                        using var insertCmd = connection.CreateCommand();
                        insertCmd.Transaction = transaction;
                        insertCmd.CommandText = @"
                            INSERT OR REPLACE INTO CollectionMembership (CollectionId, WorkstationId)
                            VALUES ($colId, $wsId);";
                        insertCmd.Parameters.Add(CreateParam(insertCmd, "$colId", col.CollectionId));
                        insertCmd.Parameters.Add(CreateParam(insertCmd, "$wsId", workstation.WorkstationId));
                        await insertCmd.ExecuteNonQueryAsync(ct);
                    }
                    else
                    {
                        using var deleteCmd = connection.CreateCommand();
                        deleteCmd.Transaction = transaction;
                        deleteCmd.CommandText = @"
                            DELETE FROM CollectionMembership
                            WHERE CollectionId = $colId AND WorkstationId = $wsId;";
                        deleteCmd.Parameters.Add(CreateParam(deleteCmd, "$colId", col.CollectionId));
                        deleteCmd.Parameters.Add(CreateParam(deleteCmd, "$wsId", workstation.WorkstationId));
                        await deleteCmd.ExecuteNonQueryAsync(ct);
                    }
                }

                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to evaluate collection membership for workstation '{WorkstationId}'", workstation.WorkstationId);
                throw;
            }
        }

        private bool EvaluateRule(Workstation ws, string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return false;

            var parts = expression.Split(new[] { "==", ">=", "<=", ">", "<", "!=" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;

            string field = parts[0].ToUpperInvariant();
            string val = parts[1].Trim('"', '\'');

            string op = "";
            if (expression.Contains("==")) op = "==";
            else if (expression.Contains(">=")) op = ">=";
            else if (expression.Contains("<=")) op = "<=";
            else if (expression.Contains(">")) op = ">";
            else if (expression.Contains("<")) op = "<";
            else if (expression.Contains("!=")) op = "!=";

            switch (field)
            {
                case "GPU":
                    return CompareString(ws.Gpu, val, op);
                case "RAM":
                    int.TryParse(val.Replace("GB", "", StringComparison.OrdinalIgnoreCase).Trim(), out int valInt);
                    return CompareNumeric(ws.RamGb, valInt, op);
                case "WINDOWSVERSION":
                case "WINDOWS VERSION":
                    return CompareString(ws.WindowsVersion, val, op);
                case "POLICYVERSION":
                case "POLICY VERSION":
                    return CompareString(ws.PolicyVersion, val, op);
                case "HEALTHSTATE":
                case "HEALTH STATE":
                    return CompareString(ws.HealthState, val, op);
                case "STATUS":
                    return CompareString(ws.Status, val, op);
                default:
                    return false;
            }
        }

        private bool CompareString(string actual, string expected, string op)
        {
            actual ??= "";
            expected ??= "";
            if (op == "==") return actual.Equals(expected, StringComparison.OrdinalIgnoreCase) || actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
            if (op == "!=") return !actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        private bool CompareNumeric(double actual, double expected, string op)
        {
            if (op == "==") return Math.Abs(actual - expected) < 0.001;
            if (op == "!=") return Math.Abs(actual - expected) >= 0.001;
            if (op == ">") return actual > expected;
            if (op == "<") return actual < expected;
            if (op == ">=") return actual >= expected;
            if (op == "<=") return actual <= expected;
            return false;
        }

        #endregion
    }
}
