using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sayra.Client.Shared.Models;

namespace Sayra.UI.Notifications.Services
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _dbLock = new(1, 1);

        public NotificationRepository(string dbName = "notifications_history.db")
        {
            var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            string dbPath = Path.Combine(dataDir, dbName);
            _connectionString = $"Data Source={dbPath};Cache=Shared";
        }

        public async Task InitializeAsync()
        {
            await _dbLock.WaitAsync();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Notifications (
                        Id TEXT PRIMARY KEY NOT NULL,
                        Title TEXT NOT NULL,
                        Body TEXT NOT NULL,
                        Category TEXT NOT NULL,
                        Priority TEXT NOT NULL,
                        TtlSeconds INTEGER NOT NULL,
                        ActionCallbackToken TEXT,
                        LanguageToken TEXT NOT NULL,
                        TemplateArgs TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        IsRead INTEGER DEFAULT 0,
                        Signature TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS IDX_Notifications_Priority_CreatedAt ON Notifications(Priority, CreatedAt);";
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task SaveNotificationAsync(NotificationPayload notification)
        {
            if (notification == null) return;

            await _dbLock.WaitAsync();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO Notifications (Id, Title, Body, Category, Priority, TtlSeconds, ActionCallbackToken, LanguageToken, TemplateArgs, CreatedAt, Signature)
                    VALUES ($Id, $Title, $Body, $Category, $Priority, $TtlSeconds, $ActionCallbackToken, $LanguageToken, $TemplateArgs, $CreatedAt, $Signature);";

                cmd.Parameters.AddWithValue("$Id", notification.Id.ToString());
                cmd.Parameters.AddWithValue("$Title", notification.Title);
                cmd.Parameters.AddWithValue("$Body", notification.Body);
                cmd.Parameters.AddWithValue("$Category", notification.Category.ToString());
                cmd.Parameters.AddWithValue("$Priority", notification.Priority.ToString());
                cmd.Parameters.AddWithValue("$TtlSeconds", notification.TtlSeconds);
                cmd.Parameters.AddWithValue("$ActionCallbackToken", (object?)notification.ActionCallbackToken ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$LanguageToken", notification.LanguageToken);
                cmd.Parameters.AddWithValue("$TemplateArgs", JsonSerializer.Serialize(notification.TemplateArgs));
                cmd.Parameters.AddWithValue("$CreatedAt", notification.CreatedAt.ToString("O"));
                cmd.Parameters.AddWithValue("$Signature", notification.Signature);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task<List<NotificationPayload>> GetNotificationsAsync(
            string? searchQuery = null,
            NotificationPriority? priorityFilter = null,
            NotificationCategory? categoryFilter = null)
        {
            var results = new List<NotificationPayload>();

            await _dbLock.WaitAsync();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                var query = "SELECT Id, Title, Body, Category, Priority, TtlSeconds, ActionCallbackToken, LanguageToken, TemplateArgs, CreatedAt, Signature FROM Notifications WHERE 1=1";

                if (!string.IsNullOrEmpty(searchQuery))
                {
                    query += " AND (Title LIKE $search OR Body LIKE $search)";
                    cmd.Parameters.AddWithValue("$search", $"%{searchQuery}%");
                }

                if (priorityFilter.HasValue)
                {
                    query += " AND Priority = $priority";
                    cmd.Parameters.AddWithValue("$priority", priorityFilter.Value.ToString());
                }

                if (categoryFilter.HasValue)
                {
                    query += " AND Category = $category";
                    cmd.Parameters.AddWithValue("$category", categoryFilter.Value.ToString());
                }

                query += " ORDER BY CreatedAt DESC";
                cmd.CommandText = query;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = Guid.Parse(reader.GetString(0));
                    var title = reader.GetString(1);
                    var body = reader.GetString(2);
                    var category = Enum.Parse<NotificationCategory>(reader.GetString(3));
                    var priority = Enum.Parse<NotificationPriority>(reader.GetString(4));
                    var ttl = reader.GetInt32(5);
                    var callback = reader.IsDBNull(6) ? null : reader.GetString(6);
                    var langToken = reader.GetString(7);
                    var templateArgsJson = reader.GetString(8);
                    var createdAt = DateTime.Parse(reader.GetString(9));
                    var sig = reader.GetString(10);

                    var templateArgs = JsonSerializer.Deserialize<Dictionary<string, string>>(templateArgsJson) ?? new();

                    results.Add(new NotificationPayload
                    {
                        Id = id,
                        Title = title,
                        Body = body,
                        Category = category,
                        Priority = priority,
                        TtlSeconds = ttl,
                        ActionCallbackToken = callback,
                        LanguageToken = langToken,
                        TemplateArgs = templateArgs,
                        CreatedAt = createdAt,
                        Signature = sig
                    });
                }
            }
            finally
            {
                _dbLock.Release();
            }

            return results;
        }

        public async Task MarkAsReadAsync(Guid id)
        {
            await _dbLock.WaitAsync();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE Notifications SET IsRead = 1 WHERE Id = $id";
                cmd.Parameters.AddWithValue("$id", id.ToString());
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task MarkAllAsReadAsync()
        {
            await _dbLock.WaitAsync();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE Notifications SET IsRead = 1";
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task DeleteNotificationAsync(Guid id)
        {
            await _dbLock.WaitAsync();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM Notifications WHERE Id = $id";
                cmd.Parameters.AddWithValue("$id", id.ToString());
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task ClearAllAsync()
        {
            await _dbLock.WaitAsync();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM Notifications";
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _dbLock.Release();
            }
        }
    }
}
