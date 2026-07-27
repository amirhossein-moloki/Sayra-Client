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

        public async Task RegisterWorkstationAsync(string machineId, Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentNullException(nameof(machineId));
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Registering workstation '{MachineId}' with metadata...", machineId);

                using var connection = _databaseService.CreateConnection();
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Workstations (MachineId, Status, MetadataJson, RegisteredAt, LastSeenAt)
                    VALUES ($id, $status, $meta, $regAt, $lastSeen)
                    ON CONFLICT(MachineId) DO UPDATE SET
                        MetadataJson = excluded.MetadataJson,
                        LastSeenAt = excluded.LastSeenAt;";

                cmd.Parameters.Add(CreateParam(cmd, "$id", machineId));
                cmd.Parameters.Add(CreateParam(cmd, "$status", "Online"));
                cmd.Parameters.Add(CreateParam(cmd, "$meta", JsonSerializer.Serialize(metadata)));
                cmd.Parameters.Add(CreateParam(cmd, "$regAt", DateTime.UtcNow.ToString("O")));
                cmd.Parameters.Add(CreateParam(cmd, "$lastSeen", DateTime.UtcNow.ToString("O")));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task UpdateWorkstationMetadataAsync(string machineId, Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentNullException(nameof(machineId));
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Updating metadata for workstation '{MachineId}'...", machineId);

                using var connection = _databaseService.CreateConnection();
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Workstations
                    SET MetadataJson = $meta, LastSeenAt = $lastSeen
                    WHERE MachineId = $id;";

                cmd.Parameters.Add(CreateParam(cmd, "$id", machineId));
                cmd.Parameters.Add(CreateParam(cmd, "$meta", JsonSerializer.Serialize(metadata)));
                cmd.Parameters.Add(CreateParam(cmd, "$lastSeen", DateTime.UtcNow.ToString("O")));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task AssignWorkstationToGroupsAsync(string machineId, List<string> groupIds, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentNullException(nameof(machineId));
            if (groupIds == null) throw new ArgumentNullException(nameof(groupIds));

            _logger.LogInformation("Assigning workstation '{MachineId}' to multiple groups...", machineId);

            foreach (var groupId in groupIds)
            {
                await _groupRepository.AssignMachineAsync(machineId, groupId, cancellationToken);
            }
        }

        public async Task RemoveWorkstationAsync(string machineId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentNullException(nameof(machineId));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Removing workstation '{MachineId}' from fleet...", machineId);

                using var connection = _databaseService.CreateConnection();
                await connection.OpenAsync(cancellationToken);

                using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM Workstations WHERE MachineId = $id;";
                        cmd.Parameters.Add(CreateParam(cmd, "$id", machineId));
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM MachineAssignments WHERE MachineId = $id;";
                        cmd.Parameters.Add(CreateParam(cmd, "$id", machineId));
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM CollectionMembership WHERE MachineId = $id;";
                        cmd.Parameters.Add(CreateParam(cmd, "$id", machineId));
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Failed to remove workstation '{MachineId}' safely.", machineId);
                    throw;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<string> QueryWorkstationStatusAsync(string machineId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentNullException(nameof(machineId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Status FROM Workstations WHERE MachineId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", machineId));

            var res = await cmd.ExecuteScalarAsync(cancellationToken);
            return res?.ToString() ?? "Unknown";
        }

        public async Task<Dictionary<string, string>> QueryWorkstationCapabilitiesAsync(string machineId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentNullException(nameof(machineId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MetadataJson FROM Workstations WHERE MachineId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", machineId));

            var res = await cmd.ExecuteScalarAsync(cancellationToken);
            if (res != null)
            {
                try
                {
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(res.ToString()!) ?? new();
                }
                catch
                {
                    return new();
                }
            }
            return new();
        }

        public async Task<List<string>> GetAllRegisteredWorkstationsAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MachineId FROM Workstations;";

            var list = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(reader.GetString(0));
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
