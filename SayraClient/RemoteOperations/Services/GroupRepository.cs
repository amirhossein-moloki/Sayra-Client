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
    public class GroupRepository : IGroupRepository
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly ILogger<GroupRepository> _logger;

        public GroupRepository(
            ILocalDatabaseService databaseService,
            ILogger<GroupRepository> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CreateGroupAsync(MachineGroup group, CancellationToken ct = default)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            _logger.LogInformation("Creating machine group '{GroupId}' ({Name})", group.GroupId, group.Name);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO MachineGroups (GroupId, Name, Description, GroupType)
                    VALUES ($groupId, $name, $desc, $type);";

                cmd.Parameters.Add(CreateParam(cmd, "$groupId", group.GroupId));
                cmd.Parameters.Add(CreateParam(cmd, "$name", group.Name));
                cmd.Parameters.Add(CreateParam(cmd, "$desc", group.Description));
                cmd.Parameters.Add(CreateParam(cmd, "$type", group.GroupType));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to create machine group '{GroupId}'.", group.GroupId);
                throw;
            }
        }

        public async Task DeleteGroupAsync(string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("Group ID cannot be empty", nameof(groupId));

            _logger.LogInformation("Deleting machine group '{GroupId}'", groupId);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM MachineAssignments WHERE GroupId = $groupId;";
                    cmd.Parameters.Add(CreateParam(cmd, "$groupId", groupId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM MachineGroups WHERE GroupId = $groupId;";
                    cmd.Parameters.Add(CreateParam(cmd, "$groupId", groupId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to delete machine group '{GroupId}'.", groupId);
                throw;
            }
        }

        public async Task AssignMachineAsync(string workstationId, string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(workstationId)) throw new ArgumentException("Workstation ID cannot be empty", nameof(workstationId));
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("Group ID cannot be empty", nameof(groupId));

            _logger.LogInformation("Assigning machine '{WorkstationId}' to group '{GroupId}'", workstationId, groupId);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO MachineAssignments (WorkstationId, GroupId)
                    VALUES ($workstationId, $groupId);";

                cmd.Parameters.Add(CreateParam(cmd, "$workstationId", workstationId));
                cmd.Parameters.Add(CreateParam(cmd, "$groupId", groupId));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to assign machine '{WorkstationId}' to group '{GroupId}'.", workstationId, groupId);
                throw;
            }
        }

        public async Task RemoveMachineAsync(string workstationId, string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(workstationId)) throw new ArgumentException("Workstation ID cannot be empty", nameof(workstationId));
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("Group ID cannot be empty", nameof(groupId));

            _logger.LogInformation("Removing machine '{WorkstationId}' from group '{GroupId}'", workstationId, groupId);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    DELETE FROM MachineAssignments
                    WHERE WorkstationId = $workstationId AND GroupId = $groupId;";

                cmd.Parameters.Add(CreateParam(cmd, "$workstationId", workstationId));
                cmd.Parameters.Add(CreateParam(cmd, "$groupId", groupId));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to remove machine '{WorkstationId}' from group '{GroupId}'.", workstationId, groupId);
                throw;
            }
        }

        public async Task<MachineGroup?> GetGroupAsync(string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("Group ID cannot be empty", nameof(groupId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT GroupId, Name, Description, GroupType FROM MachineGroups WHERE GroupId = $groupId;";
            cmd.Parameters.Add(CreateParam(cmd, "$groupId", groupId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new MachineGroup
                {
                    GroupId = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    GroupType = reader.GetString(3)
                };
            }

            return null;
        }

        public async Task<List<MachineGroup>> GetAllGroupsAsync(CancellationToken ct = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT GroupId, Name, Description, GroupType FROM MachineGroups;";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<MachineGroup>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(new MachineGroup
                {
                    GroupId = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    GroupType = reader.GetString(3)
                });
            }

            return list;
        }

        public async Task<List<Workstation>> GetMachinesAsync(string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("Group ID cannot be empty", nameof(groupId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT w.WorkstationId, w.Name, w.IpAddress, w.MacAddress, w.Status, w.LastSeen, w.Version, w.Gpu, w.RamGb, w.WindowsVersion, w.PolicyVersion, w.HealthState
                FROM Workstations w
                INNER JOIN MachineAssignments ma ON w.WorkstationId = ma.WorkstationId
                WHERE ma.GroupId = $groupId;";
            cmd.Parameters.Add(CreateParam(cmd, "$groupId", groupId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<Workstation>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(new Workstation
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
