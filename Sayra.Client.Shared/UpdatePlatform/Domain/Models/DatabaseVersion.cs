using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents an applied database migration and version record.
    /// </summary>
    public class DatabaseVersion
    {
        public int Version { get; set; }
        public string MigrationName { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    }
}
