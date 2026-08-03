using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Highly secure SQLCipher dynamic search engine for fast workstations indexing and pagination.
    /// </summary>
    public class FleetSearchEngine : IFleetSearchEngine
    {
        private readonly IFleetDatabaseContext _dbContext;
        private readonly ILogger<FleetSearchEngine> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FleetSearchEngine"/> class.
        /// </summary>
        public FleetSearchEngine(IFleetDatabaseContext dbContext, ILogger<FleetSearchEngine> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<SearchResult> SearchAsync(SearchParameters parameters, CancellationToken ct = default)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            _logger.LogInformation("Executing workstation search. Query: MachineId='{MId}', Hostname='{Host}'", parameters.MachineId, parameters.Hostname);

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            // Construct dynamic SQL filter predicates
            var sqlBuilder = new StringBuilder();
            var countSqlBuilder = new StringBuilder();
            var whereClauses = new List<string>();
            var sqliteParams = new List<SqliteParameter>();

            sqlBuilder.Append(@"
                SELECT w.MachineId, w.Hostname, w.IpAddress, w.MacAddress, w.Status, w.HealthStatus, w.LastSeenUtc,
                       w.SemVer, w.BuildHash, w.BuildDate,
                       i.CpuName, i.GpuName, i.RamGb, i.OperatingSystem, i.StorageDrivesJson
                FROM Workstations w
                LEFT JOIN Inventory i ON w.MachineId = i.MachineId");

            countSqlBuilder.Append(@"
                SELECT COUNT(DISTINCT w.MachineId)
                FROM Workstations w
                LEFT JOIN Inventory i ON w.MachineId = i.MachineId");

            // Filter: MachineId (Partial match)
            if (!string.IsNullOrEmpty(parameters.MachineId))
            {
                whereClauses.Add("w.MachineId LIKE $mId");
            }

            // Filter: Hostname (Partial match)
            if (!string.IsNullOrEmpty(parameters.Hostname))
            {
                whereClauses.Add("w.Hostname LIKE $host");
            }

            // Filter: Status
            if (!string.IsNullOrEmpty(parameters.Status))
            {
                whereClauses.Add("w.Status = $status");
            }

            // Filter: HealthStatus
            if (!string.IsNullOrEmpty(parameters.HealthStatus))
            {
                whereClauses.Add("w.HealthStatus = $hStatus");
            }

            // Filter: SemVer
            if (!string.IsNullOrEmpty(parameters.SemVer))
            {
                whereClauses.Add("w.SemVer = $semVer");
            }

            // Filter: TagKey & TagValue
            if (!string.IsNullOrEmpty(parameters.TagKey) || !string.IsNullOrEmpty(parameters.TagValue))
            {
                sqlBuilder.Append(" JOIN Tags t ON w.MachineId = t.MachineId");
                countSqlBuilder.Append(" JOIN Tags t ON w.MachineId = t.MachineId");

                if (!string.IsNullOrEmpty(parameters.TagKey))
                {
                    whereClauses.Add("t.Key = $tagKey");
                }
                if (!string.IsNullOrEmpty(parameters.TagValue))
                {
                    whereClauses.Add("t.Value = $tagValue");
                }
            }

            // Filter: Capability (evaluated dynamically on GPU / RAM / CPU specifications)
            if (!string.IsNullOrEmpty(parameters.Capability))
            {
                whereClauses.Add("(i.CpuName LIKE $cap OR i.GpuName LIKE $cap OR i.OperatingSystem LIKE $cap)");
            }

            // Construct WHERE block
            if (whereClauses.Count > 0)
            {
                var whereStr = " WHERE " + string.Join(" AND ", whereClauses);
                sqlBuilder.Append(whereStr);
                countSqlBuilder.Append(whereStr);
            }

            // 1. Execute COUNT Query
            int totalCount = 0;
            using (var countCmd = connection.CreateCommand())
            {
                countCmd.CommandText = countSqlBuilder.ToString();
                var countParams = BuildParamsList(parameters);
                foreach (var p in countParams)
                {
                    countCmd.Parameters.Add(p);
                }
                totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));
            }

            // 2. Sorting & Pagination
            string sortByCol = "w.Hostname";
            if (!string.IsNullOrEmpty(parameters.SortBy))
            {
                sortByCol = parameters.SortBy.ToLower() switch
                {
                    "machineid" => "w.MachineId",
                    "hostname" => "w.Hostname",
                    "lastseen" => "w.LastSeenUtc",
                    "status" => "w.Status",
                    "health" => "w.HealthStatus",
                    _ => "w.Hostname"
                };
            }

            string orderDir = parameters.SortDescending ? "DESC" : "ASC";
            sqlBuilder.Append($" ORDER BY {sortByCol} {orderDir}");

            // Pagination limit & offset
            sqlBuilder.Append(" LIMIT $limit OFFSET $offset;");

            // 3. Execute Query
            var items = new List<MachineInfo>();
            using (var searchCmd = connection.CreateCommand())
            {
                searchCmd.CommandText = sqlBuilder.ToString();
                var searchParams = BuildParamsList(parameters);
                searchParams.Add(new SqliteParameter("$limit", parameters.PageSize));
                searchParams.Add(new SqliteParameter("$offset", parameters.PageIndex * parameters.PageSize));

                foreach (var p in searchParams)
                {
                    searchCmd.Parameters.Add(p);
                }

                using var reader = await searchCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    items.Add(ReadMachine(reader));
                }
            }

            return new SearchResult
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = parameters.PageIndex,
                PageSize = parameters.PageSize
            };
        }

        private static MachineInfo ReadMachine(DbDataReader reader)
        {
            string machineId = reader.GetString(0);
            string hostname = reader.GetString(1);
            string ipAddress = reader.GetString(2);
            string macAddress = reader.GetString(3);

            Enum.TryParse<MachineStatus>(reader.GetString(4), out var status);
            Enum.TryParse<MachineHealthStatus>(reader.GetString(5), out var healthStatus);

            DateTime lastSeenUtc = reader.GetDateTime(6);

            var version = new MachineVersion
            {
                SemVer = reader.GetString(7),
                BuildHash = reader.GetString(8),
                BuildDate = reader.GetDateTime(9)
            };

            var inventory = new MachineInventory();
            if (!reader.IsDBNull(10))
            {
                var drivesJson = reader.GetString(14);
                var drives = JsonSerializer.Deserialize<Dictionary<string, string>>(drivesJson) ?? new Dictionary<string, string>();

                inventory = new MachineInventory
                {
                    CpuName = reader.GetString(10),
                    GpuName = reader.GetString(11),
                    RamGb = reader.GetInt32(12),
                    OperatingSystem = reader.GetString(13),
                    StorageDrives = drives
                };
            }

            return new MachineInfo
            {
                MachineId = machineId,
                Hostname = hostname,
                IpAddress = ipAddress,
                MacAddress = macAddress,
                Status = status,
                HealthStatus = healthStatus,
                LastSeenUtc = lastSeenUtc,
                Version = version,
                Inventory = inventory
            };
        }

        private static List<SqliteParameter> BuildParamsList(SearchParameters parameters)
        {
            var list = new List<SqliteParameter>();

            if (!string.IsNullOrEmpty(parameters.MachineId))
            {
                list.Add(new SqliteParameter("$mId", $"%{parameters.MachineId}%"));
            }
            if (!string.IsNullOrEmpty(parameters.Hostname))
            {
                list.Add(new SqliteParameter("$host", $"%{parameters.Hostname}%"));
            }
            if (!string.IsNullOrEmpty(parameters.Status))
            {
                list.Add(new SqliteParameter("$status", parameters.Status));
            }
            if (!string.IsNullOrEmpty(parameters.HealthStatus))
            {
                list.Add(new SqliteParameter("$hStatus", parameters.HealthStatus));
            }
            if (!string.IsNullOrEmpty(parameters.SemVer))
            {
                list.Add(new SqliteParameter("$semVer", parameters.SemVer));
            }
            if (!string.IsNullOrEmpty(parameters.TagKey))
            {
                list.Add(new SqliteParameter("$tagKey", parameters.TagKey));
            }
            if (!string.IsNullOrEmpty(parameters.TagValue))
            {
                list.Add(new SqliteParameter("$tagValue", parameters.TagValue));
            }
            if (!string.IsNullOrEmpty(parameters.Capability))
            {
                list.Add(new SqliteParameter("$cap", $"%{parameters.Capability}%"));
            }

            return list;
        }
    }
}
