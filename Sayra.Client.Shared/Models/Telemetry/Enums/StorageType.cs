namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Enumerates historical and telemetry storage target types.
    /// </summary>
    public enum StorageType
    {
        /// <summary>Temporary thread-safe in-memory cache.</summary>
        Memory,
        /// <summary>Persistent local SQLite/SQLCipher database.</summary>
        SQLite,
        /// <summary>Flat log or structured diagnostic file.</summary>
        File,
        /// <summary>Remote management console or cloud server API.</summary>
        Remote
    }
}
