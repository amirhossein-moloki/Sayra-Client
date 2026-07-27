using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services.Fleet
{
    public class GroupRepository : IGroupRepository
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly ILogger<GroupRepository> _logger;

        public GroupRepository(ILocalDatabaseService databaseService, ILogger<GroupRepository> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CreateGroupAsync(MachineGroup group, CancellationToken cancellationToken = default)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO MachineGroups (GroupId, Name, Description, IsDynamic, CreatedAt, ParentGroupId)
                VALUES ($id, $name, $desc, $isDynamic, $createdAt, $parent);";

            cmd.Parameters.Add(CreateParam(cmd, "$id", group.GroupId));
            cmd.Parameters.Add(CreateParam(cmd, "$name", group.Name));
            cmd.Parameters.Add(CreateParam(cmd, "$desc", group.Description));
            cmd.Parameters.Add(CreateParam(cmd, "$isDynamic", group.IsDynamic ? 1 : 0));
            cmd.Parameters.Add(CreateParam(cmd, "$createdAt", group.CreatedAt.ToString("O")));
            cmd.Parameters.Add(CreateParam(cmd, "$parent", (object?)group.ParentGroupId ?? DBNull.Value));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task DeleteGroupAsync(string groupId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentNullException(nameof(groupId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM MachineGroups WHERE GroupId = $id;";
                    cmd.Parameters.Add(CreateParam(cmd, "$id", groupId));
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM MachineAssignments WHERE GroupId = $id;";
                    cmd.Parameters.Add(CreateParam(cmd, "$id", groupId));
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to delete group '{GroupId}' within transaction.", groupId);
                throw;
            }
        }

        public async Task AssignMachineAsync(string machineId, string groupId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentNullException(nameof(machineId));
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentNullException(nameof(groupId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR IGNORE INTO MachineAssignments (AssignmentId, MachineId, GroupId, AssignedAt)
                VALUES ($id, $machineId, $groupId, $assignedAt);";

            cmd.Parameters.Add(CreateParam(cmd, "$id", Guid.NewGuid().ToString()));
            cmd.Parameters.Add(CreateParam(cmd, "$machineId", machineId));
            cmd.Parameters.Add(CreateParam(cmd, "$groupId", groupId));
            cmd.Parameters.Add(CreateParam(cmd, "$assignedAt", DateTime.UtcNow.ToString("O")));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task RemoveMachineAsync(string machineId, string groupId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentNullException(nameof(machineId));
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentNullException(nameof(groupId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM MachineAssignments WHERE MachineId = $machineId AND GroupId = $groupId;";
            cmd.Parameters.Add(CreateParam(cmd, "$machineId", machineId));
            cmd.Parameters.Add(CreateParam(cmd, "$groupId", groupId));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<MachineGroup?> GetGroupAsync(string groupId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentNullException(nameof(groupId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT GroupId, Name, Description, IsDynamic, CreatedAt, ParentGroupId
                FROM MachineGroups
                WHERE GroupId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", groupId));

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new MachineGroup
                {
                    GroupId = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    IsDynamic = reader.GetInt32(3) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    ParentGroupId = reader.IsDBNull(5) ? (string?)null : reader.GetString(5)
                };
            }
            return null;
        }

        public async Task<List<string>> GetMachinesAsync(string groupId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentNullException(nameof(groupId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MachineId FROM MachineAssignments WHERE GroupId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", groupId));

            var list = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(reader.GetString(0));
            }
            return list;
        }

        public async Task<List<MachineGroup>> GetGroupsForMachineAsync(string machineId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentNullException(nameof(machineId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT g.GroupId, g.Name, g.Description, g.IsDynamic, g.CreatedAt, g.ParentGroupId
                FROM MachineGroups g
                INNER JOIN MachineAssignments a ON g.GroupId = a.GroupId
                WHERE a.MachineId = $machineId;";
            cmd.Parameters.Add(CreateParam(cmd, "$machineId", machineId));

            var list = new List<MachineGroup>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new MachineGroup
                {
                    GroupId = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    IsDynamic = reader.GetInt32(3) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    ParentGroupId = reader.IsDBNull(5) ? (string?)null : reader.GetString(5)
                });
            }
            return list;
        }

        public async Task<List<MachineGroup>> GetAllGroupsAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT GroupId, Name, Description, IsDynamic, CreatedAt, ParentGroupId FROM MachineGroups;";

            var list = new List<MachineGroup>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new MachineGroup
                {
                    GroupId = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    IsDynamic = reader.GetInt32(3) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    ParentGroupId = reader.IsDBNull(5) ? (string?)null : reader.GetString(5)
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
