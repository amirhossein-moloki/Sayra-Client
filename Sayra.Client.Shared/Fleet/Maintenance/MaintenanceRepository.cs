using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Fleet.Maintenance.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Maintenance
{
    /// <summary>
    /// SQLCipher-secured SQLite implementation of <see cref="IMaintenanceRepository"/>.
    /// </summary>
    public class MaintenanceRepository : IMaintenanceRepository
    {
        private readonly IFleetDatabaseContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="MaintenanceRepository"/> class.
        /// </summary>
        public MaintenanceRepository(IFleetDatabaseContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc />
        public async Task<bool> SaveScheduleAsync(MaintenanceSchedule schedule, CancellationToken ct = default)
        {
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO MaintenanceSchedules (ScheduleId, WindowId, Category, StartTimeUtc, DurationMs, ForceSessionTermination, ScopeFilter, State, ExecutionSummary)
                VALUES ($id, $windowId, $category, $start, $duration, $force, $scope, $state, $summary)
                ON CONFLICT(ScheduleId) DO UPDATE SET
                    WindowId = excluded.WindowId,
                    Category = excluded.Category,
                    StartTimeUtc = excluded.StartTimeUtc,
                    DurationMs = excluded.DurationMs,
                    ForceSessionTermination = excluded.ForceSessionTermination,
                    ScopeFilter = excluded.ScopeFilter,
                    State = excluded.State,
                    ExecutionSummary = excluded.ExecutionSummary;";

            cmd.Parameters.Add(new SqliteParameter("$id", schedule.ScheduleId));
            cmd.Parameters.Add(new SqliteParameter("$windowId", schedule.Window?.WindowId ?? string.Empty));
            cmd.Parameters.Add(new SqliteParameter("$category", schedule.Window?.Category.ToString() ?? MaintenanceWindowType.SystemCleanup.ToString()));
            cmd.Parameters.Add(new SqliteParameter("$start", schedule.Window?.StartTimeUtc.ToString("O") ?? DateTime.UtcNow.ToString("O")));
            cmd.Parameters.Add(new SqliteParameter("$duration", (long)(schedule.Window?.Duration.TotalMilliseconds ?? 0)));
            cmd.Parameters.Add(new SqliteParameter("$force", (schedule.Window?.ForceSessionTermination ?? false) ? 1 : 0));
            cmd.Parameters.Add(new SqliteParameter("$scope", schedule.ScopeFilter ?? string.Empty));
            cmd.Parameters.Add(new SqliteParameter("$state", schedule.State.ToString()));
            cmd.Parameters.Add(new SqliteParameter("$summary", schedule.ExecutionSummary ?? string.Empty));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<MaintenanceSchedule?> GetScheduleAsync(string scheduleId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(scheduleId)) return null;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT ScheduleId, WindowId, Category, StartTimeUtc, DurationMs, ForceSessionTermination, ScopeFilter, State, ExecutionSummary
                FROM MaintenanceSchedules
                WHERE ScheduleId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", scheduleId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return ReadSchedule(reader);
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteScheduleAsync(string scheduleId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(scheduleId)) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM MaintenanceSchedules WHERE ScheduleId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", scheduleId));

            int rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MaintenanceSchedule>> GetAllSchedulesAsync(CancellationToken ct = default)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT ScheduleId, WindowId, Category, StartTimeUtc, DurationMs, ForceSessionTermination, ScopeFilter, State, ExecutionSummary
                FROM MaintenanceSchedules;";

            var list = new List<MaintenanceSchedule>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(ReadSchedule(reader));
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<bool> SaveExecutionAsync(MaintenanceExecution execution, CancellationToken ct = default)
        {
            if (execution == null) throw new ArgumentNullException(nameof(execution));

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO MaintenanceExecutions (ExecutionId, ScheduleId, MachineId, Status, StartTimeUtc, EndTimeUtc, OutputLogs, ErrorMessage)
                VALUES ($id, $scheduleId, $machineId, $status, $start, $end, $logs, $err)
                ON CONFLICT(ExecutionId) DO UPDATE SET
                    ScheduleId = excluded.ScheduleId,
                    MachineId = excluded.MachineId,
                    Status = excluded.Status,
                    StartTimeUtc = excluded.StartTimeUtc,
                    EndTimeUtc = excluded.EndTimeUtc,
                    OutputLogs = excluded.OutputLogs,
                    ErrorMessage = excluded.ErrorMessage;";

            cmd.Parameters.Add(new SqliteParameter("$id", execution.ExecutionId));
            cmd.Parameters.Add(new SqliteParameter("$scheduleId", execution.ScheduleId));
            cmd.Parameters.Add(new SqliteParameter("$machineId", execution.MachineId));
            cmd.Parameters.Add(new SqliteParameter("$status", execution.Status));
            cmd.Parameters.Add(new SqliteParameter("$start", execution.StartTimeUtc?.ToString("O") ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqliteParameter("$end", execution.EndTimeUtc?.ToString("O") ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqliteParameter("$logs", execution.OutputLogs ?? string.Empty));
            cmd.Parameters.Add(new SqliteParameter("$err", execution.ErrorMessage ?? string.Empty));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<MaintenanceExecution?> GetExecutionAsync(string executionId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(executionId)) return null;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT ExecutionId, ScheduleId, MachineId, Status, StartTimeUtc, EndTimeUtc, OutputLogs, ErrorMessage
                FROM MaintenanceExecutions
                WHERE ExecutionId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", executionId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return ReadExecution(reader);
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MaintenanceExecution>> GetExecutionsByScheduleAsync(string scheduleId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(scheduleId)) return Array.Empty<MaintenanceExecution>();

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT ExecutionId, ScheduleId, MachineId, Status, StartTimeUtc, EndTimeUtc, OutputLogs, ErrorMessage
                FROM MaintenanceExecutions
                WHERE ScheduleId = $scheduleId;";
            cmd.Parameters.Add(new SqliteParameter("$scheduleId", scheduleId));

            var list = new List<MaintenanceExecution>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(ReadExecution(reader));
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MaintenanceExecution>> GetExecutionsByMachineAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return Array.Empty<MaintenanceExecution>();

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT ExecutionId, ScheduleId, MachineId, Status, StartTimeUtc, EndTimeUtc, OutputLogs, ErrorMessage
                FROM MaintenanceExecutions
                WHERE MachineId = $machineId;";
            cmd.Parameters.Add(new SqliteParameter("$machineId", machineId));

            var list = new List<MaintenanceExecution>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(ReadExecution(reader));
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<bool> RecordHistoryAsync(MaintenanceHistory history, CancellationToken ct = default)
        {
            if (history == null) throw new ArgumentNullException(nameof(history));

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO MaintenanceHistory (HistoryId, ScheduleId, OutcomeStatus, AffectedMachinesJson, StartTimeUtc, EndTimeUtc, Summary)
                VALUES ($id, $scheduleId, $outcome, $machines, $start, $end, $summary);";

            string machinesJson = JsonSerializer.Serialize(history.AffectedMachines ?? new List<string>());

            cmd.Parameters.Add(new SqliteParameter("$id", history.HistoryId));
            cmd.Parameters.Add(new SqliteParameter("$scheduleId", history.ScheduleId));
            cmd.Parameters.Add(new SqliteParameter("$outcome", history.OutcomeStatus));
            cmd.Parameters.Add(new SqliteParameter("$machines", machinesJson));
            cmd.Parameters.Add(new SqliteParameter("$start", history.StartTimeUtc.ToString("O")));
            cmd.Parameters.Add(new SqliteParameter("$end", history.EndTimeUtc.ToString("O")));
            cmd.Parameters.Add(new SqliteParameter("$summary", history.Summary ?? string.Empty));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MaintenanceHistory>> GetHistoryAsync(CancellationToken ct = default)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT HistoryId, ScheduleId, OutcomeStatus, AffectedMachinesJson, StartTimeUtc, EndTimeUtc, Summary
                FROM MaintenanceHistory
                ORDER BY EndTimeUtc DESC;";

            var list = new List<MaintenanceHistory>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                string historyId = reader.GetString(0);
                string scheduleId = reader.GetString(1);
                string outcome = reader.GetString(2);
                string machinesJson = reader.GetString(3);
                DateTime start = DateTime.Parse(reader.GetString(4));
                DateTime end = DateTime.Parse(reader.GetString(5));
                string summary = reader.GetString(6);

                var machines = JsonSerializer.Deserialize<List<string>>(machinesJson) ?? new List<string>();

                list.Add(new MaintenanceHistory
                {
                    HistoryId = historyId,
                    ScheduleId = scheduleId,
                    OutcomeStatus = outcome,
                    AffectedMachines = machines,
                    StartTimeUtc = start,
                    EndTimeUtc = end,
                    Summary = summary
                });
            }

            return list;
        }

        private static MaintenanceSchedule ReadSchedule(DbDataReader reader)
        {
            string scheduleId = reader.GetString(0);
            string windowId = reader.GetString(1);

            Enum.TryParse<MaintenanceWindowType>(reader.GetString(2), true, out var category);
            DateTime startTime = DateTime.Parse(reader.GetString(3));
            TimeSpan duration = TimeSpan.FromMilliseconds(reader.GetInt64(4));
            bool force = reader.GetInt32(5) == 1;

            string scope = reader.GetString(6);
            Enum.TryParse<MaintenanceStatus>(reader.GetString(7), true, out var state);
            string summary = reader.GetString(8);

            var window = new MaintenanceWindow
            {
                WindowId = windowId,
                Category = category,
                StartTimeUtc = startTime,
                Duration = duration,
                ForceSessionTermination = force
            };

            return new MaintenanceSchedule
            {
                ScheduleId = scheduleId,
                Window = window,
                ScopeFilter = scope,
                State = state,
                ExecutionSummary = summary
            };
        }

        private static MaintenanceExecution ReadExecution(DbDataReader reader)
        {
            string executionId = reader.GetString(0);
            string scheduleId = reader.GetString(1);
            string machineId = reader.GetString(2);
            string status = reader.GetString(3);

            DateTime? start = null;
            if (!reader.IsDBNull(4))
            {
                start = DateTime.Parse(reader.GetString(4));
            }

            DateTime? end = null;
            if (!reader.IsDBNull(5))
            {
                end = DateTime.Parse(reader.GetString(5));
            }

            string logs = reader.GetString(6);
            string err = reader.GetString(7);

            return new MaintenanceExecution
            {
                ExecutionId = executionId,
                ScheduleId = scheduleId,
                MachineId = machineId,
                Status = status,
                StartTimeUtc = start,
                EndTimeUtc = end,
                OutputLogs = logs,
                ErrorMessage = err
            };
        }
    }
}
