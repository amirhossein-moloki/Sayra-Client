using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Infrastructure
{
    /// <summary>
    /// Highly secure SQLCipher implementation of <see cref="IGroupRepository"/>.
    /// </summary>
    public class GroupRepository : IGroupRepository
    {
        private readonly IFleetDatabaseContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupRepository"/> class.
        /// </summary>
        public GroupRepository(IFleetDatabaseContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc />
        public async Task<bool> SaveGroupAsync(FleetGroup group, CancellationToken ct = default)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Groups (GroupId, Name, Description, GroupType, DynamicRuleExpression, ParentGroupId)
                VALUES ($id, $name, $description, $groupType, $rule, $parent)
                ON CONFLICT(GroupId) DO UPDATE SET
                    Name = excluded.Name,
                    Description = excluded.Description,
                    GroupType = excluded.GroupType,
                    DynamicRuleExpression = excluded.DynamicRuleExpression,
                    ParentGroupId = excluded.ParentGroupId;";

            cmd.Parameters.Add(new SqliteParameter("$id", group.GroupId));
            cmd.Parameters.Add(new SqliteParameter("$name", group.Name));
            cmd.Parameters.Add(new SqliteParameter("$description", group.Description));
            cmd.Parameters.Add(new SqliteParameter("$groupType", group.GroupType.ToString()));
            cmd.Parameters.Add(new SqliteParameter("$rule", group.DynamicRuleExpression ?? string.Empty));
            cmd.Parameters.Add(new SqliteParameter("$parent", DBNull.Value)); // Standard group has parent optionally

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteGroupAsync(string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(groupId)) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM Groups WHERE GroupId = $id;";
                    cmd.Parameters.Add(new SqliteParameter("$id", groupId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM GroupMembership WHERE GroupId = $id;";
                    cmd.Parameters.Add(new SqliteParameter("$id", groupId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await transaction.CommitAsync(ct);
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<FleetGroup?> GetGroupAsync(string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(groupId)) return null;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT GroupId, Name, Description, GroupType, DynamicRuleExpression FROM Groups WHERE GroupId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", groupId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                Enum.TryParse<FleetGroupType>(reader.GetString(3), out var groupType);
                return new FleetGroup
                {
                    GroupId = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    GroupType = groupType,
                    DynamicRuleExpression = reader.GetString(4)
                };
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<FleetGroup>> GetAllGroupsAsync(CancellationToken ct = default)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT GroupId, Name, Description, GroupType, DynamicRuleExpression FROM Groups;";

            var list = new List<FleetGroup>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                Enum.TryParse<FleetGroupType>(reader.GetString(3), out var groupType);
                list.Add(new FleetGroup
                {
                    GroupId = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    GroupType = groupType,
                    DynamicRuleExpression = reader.GetString(4)
                });
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<bool> AssignMachineAsync(string machineId, string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(groupId)) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO GroupMembership (GroupId, MachineId) VALUES ($groupId, $machineId);";
            cmd.Parameters.Add(new SqliteParameter("$groupId", groupId));
            cmd.Parameters.Add(new SqliteParameter("$machineId", machineId));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveMachineAsync(string machineId, string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(groupId)) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM GroupMembership WHERE GroupId = $groupId AND MachineId = $machineId;";
            cmd.Parameters.Add(new SqliteParameter("$groupId", groupId));
            cmd.Parameters.Add(new SqliteParameter("$machineId", machineId));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> GetMachineIdsInGroupAsync(string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(groupId)) return Array.Empty<string>();

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MachineId FROM GroupMembership WHERE GroupId = $groupId;";
            cmd.Parameters.Add(new SqliteParameter("$groupId", groupId));

            var list = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(reader.GetString(0));
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> GetGroupIdsForMachineAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return Array.Empty<string>();

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT GroupId FROM GroupMembership WHERE MachineId = $machineId;";
            cmd.Parameters.Add(new SqliteParameter("$machineId", machineId));

            var list = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(reader.GetString(0));
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<bool> SyncGroupMembershipsAsync(string groupId, IEnumerable<string> machineIds, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(groupId)) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                // Delete existing dynamic memberships for this group
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM GroupMembership WHERE GroupId = $groupId;";
                    cmd.Parameters.Add(new SqliteParameter("$groupId", groupId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // Insert new ones
                foreach (var machineId in machineIds)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = "INSERT INTO GroupMembership (GroupId, MachineId) VALUES ($groupId, $machineId);";
                    cmd.Parameters.Add(new SqliteParameter("$groupId", groupId));
                    cmd.Parameters.Add(new SqliteParameter("$machineId", machineId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await transaction.CommitAsync(ct);
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
    }
}
