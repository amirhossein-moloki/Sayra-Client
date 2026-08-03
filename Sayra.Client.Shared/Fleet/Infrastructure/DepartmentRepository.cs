using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Infrastructure
{
    /// <summary>
    /// Highly secure SQLCipher implementation of <see cref="IDepartmentRepository"/>.
    /// </summary>
    public class DepartmentRepository : IDepartmentRepository
    {
        private readonly IFleetDatabaseContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="DepartmentRepository"/> class.
        /// </summary>
        public DepartmentRepository(IFleetDatabaseContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc />
        public async Task<bool> SaveAsync(FleetDepartment department, string? parentDepartmentId, CancellationToken ct = default)
        {
            if (department == null) throw new ArgumentNullException(nameof(department));

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Departments (DepartmentId, Name, DepartmentType, ParentDepartmentId)
                VALUES ($id, $name, $type, $parent)
                ON CONFLICT(DepartmentId) DO UPDATE SET
                    Name = excluded.Name,
                    DepartmentType = excluded.DepartmentType,
                    ParentDepartmentId = excluded.ParentDepartmentId;";

            cmd.Parameters.Add(new SqliteParameter("$id", department.DepartmentId));
            cmd.Parameters.Add(new SqliteParameter("$name", department.Name));
            cmd.Parameters.Add(new SqliteParameter("$type", department.DepartmentType.ToString()));
            cmd.Parameters.Add(new SqliteParameter("$parent", (object?)parentDepartmentId ?? DBNull.Value));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string departmentId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(departmentId)) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Departments WHERE DepartmentId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", departmentId));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<FleetDepartment?> GetAsync(string departmentId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(departmentId)) return null;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT DepartmentId, Name, DepartmentType FROM Departments WHERE DepartmentId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", departmentId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                Enum.TryParse<FleetDepartmentType>(reader.GetString(2), out var type);
                return new FleetDepartment
                {
                    DepartmentId = reader.GetString(0),
                    Name = reader.GetString(1),
                    DepartmentType = type
                };
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<string?> GetParentDepartmentIdAsync(string departmentId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(departmentId)) return null;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT ParentDepartmentId FROM Departments WHERE DepartmentId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", departmentId));

            var result = await cmd.ExecuteScalarAsync(ct);
            return result == DBNull.Value ? null : result as string;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<FleetDepartment>> GetAllAsync(CancellationToken ct = default)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT DepartmentId, Name, DepartmentType FROM Departments;";

            var list = new List<FleetDepartment>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                Enum.TryParse<FleetDepartmentType>(reader.GetString(2), out var type);
                list.Add(new FleetDepartment
                {
                    DepartmentId = reader.GetString(0),
                    Name = reader.GetString(1),
                    DepartmentType = type
                });
            }

            return list;
        }
    }
}
