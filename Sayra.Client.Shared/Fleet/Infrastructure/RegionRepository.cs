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
    /// Highly secure SQLCipher implementation of <see cref="IRegionRepository"/>.
    /// </summary>
    public class RegionRepository : IRegionRepository
    {
        private readonly IFleetDatabaseContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegionRepository"/> class.
        /// </summary>
        public RegionRepository(IFleetDatabaseContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc />
        public async Task<bool> SaveAsync(FleetRegion region, string? parentRegionId, CancellationToken ct = default)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Regions (RegionId, Name, RegionType, ParentRegionId)
                VALUES ($id, $name, $type, $parent)
                ON CONFLICT(RegionId) DO UPDATE SET
                    Name = excluded.Name,
                    RegionType = excluded.RegionType,
                    ParentRegionId = excluded.ParentRegionId;";

            cmd.Parameters.Add(new SqliteParameter("$id", region.RegionId));
            cmd.Parameters.Add(new SqliteParameter("$name", region.Name));
            cmd.Parameters.Add(new SqliteParameter("$type", region.RegionType.ToString()));
            cmd.Parameters.Add(new SqliteParameter("$parent", (object?)parentRegionId ?? DBNull.Value));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string regionId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(regionId)) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Regions WHERE RegionId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", regionId));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<FleetRegion?> GetAsync(string regionId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(regionId)) return null;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT RegionId, Name, RegionType FROM Regions WHERE RegionId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", regionId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                Enum.TryParse<FleetRegionType>(reader.GetString(2), out var type);
                return new FleetRegion
                {
                    RegionId = reader.GetString(0),
                    Name = reader.GetString(1),
                    RegionType = type
                };
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<string?> GetParentRegionIdAsync(string regionId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(regionId)) return null;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT ParentRegionId FROM Regions WHERE RegionId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", regionId));

            var result = await cmd.ExecuteScalarAsync(ct);
            return result == DBNull.Value ? null : result as string;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<FleetRegion>> GetAllAsync(CancellationToken ct = default)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT RegionId, Name, RegionType FROM Regions;";

            var list = new List<FleetRegion>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                Enum.TryParse<FleetRegionType>(reader.GetString(2), out var type);
                list.Add(new FleetRegion
                {
                    RegionId = reader.GetString(0),
                    Name = reader.GetString(1),
                    RegionType = type
                });
            }

            return list;
        }
    }
}
