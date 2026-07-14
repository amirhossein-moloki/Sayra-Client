# Sayra Client Game Library Architecture (Phase 7)

## Overview
The `Sayra.Client.GameLibrary` component forms the foundation for managing local games, applications, and their corresponding configurations on each client machine. This system operates entirely database-free, utilizing high-performance asynchronous JSON storage, and supports atomic writes, corruption recovery, and robust validation.

---

## 1. Domain Models
The domain models are defined in the `Sayra.Client.GameLibrary.Models` namespace. They represent the core entities utilized by the client to register and launch games and applications.

### Game
Represents a game available on the local machine.
- `Id` (`string`): A unique identifier for the game (typically a GUID).
- `Name` (`string`): The user-facing display name of the game.
- `ExecutablePath` (`string`): The absolute path to the main game executable.
- `Arguments` (`string`): Command-line arguments passed to the executable upon launch.
- `WorkingDirectory` (`string`): The directory in which the game process should start.
- `IconPath` (`string`): Path to the game icon file.
- `Category` (`GameCategory`): The custom category classification for this game.
- `Enabled` (`bool`): Determines if the game is active and launchable by the user.
- `Source` (`GameSource`): Enum indicating how the game was added (`Manual`, `Scanner`, `Server`).
- `CreatedAt` (`DateTime`): Timestamp when the game was registered.
- `UpdatedAt` (`DateTime`): Timestamp of the last configuration modification.
- `LaunchProfile` (`LaunchProfile?`): Optional associated profiles with custom launch configurations.

### Application
Represents any general non-game utility or application (e.g., Discord, Web Browsers) running alongside Sayra.
- `Id` (`string`): Unique identifier.
- `Name` (`string`): Name of the application.
- `ExecutablePath` (`string`): Absolute path to the executable.
- `Publisher` (`string`): Publisher of the application.
- `Version` (`string`): Software version.
- `Type` (`string`): Application type categorization.

### GameCategory
Represents a category metadata object.
- `Id` (`string`): Unique category identifier.
- `Name` (`string`): Localized/display name of the category.

### LaunchProfile
Encapsulates launch-specific environmental options.
- `Arguments` (`string`): Overriding command-line parameters.
- `EnvironmentVariables` (`Dictionary<string, string>`): Custom environment variables set during process creation.
- `WorkingDirectory` (`string`): Overriding execution path.

### GameSource (Enum)
Defines the ingestion mechanism:
- `Manual`: Manually added by client administrators.
- `Scanner`: Discovered by the client's automated scanner.
- `Server`: Pushed down by the central Sayra Server.

---

## 2. Local Persistence Layer (`GameLibraryRepository`)
The persistence layer provides robust, asynchronous, and database-less storage designed to handle power-loss or unexpected crash scenarios gracefully.

- **Storage Location**: `Data/GameLibrary/`
- **Data Files**:
  - `games.json`: Serialized JSON array of `Game` objects.
  - `applications.json`: Serialized JSON array of `Application` objects.

### Safety Design Patterns:
1. **Asynchronous I/O**: High-performance asynchronous reads and writes (`FileStream` + `JsonSerializer.DeserializeAsync` / `JsonSerializer.SerializeAsync`) to guarantee non-blocking operations on the calling threads.
2. **Atomic File Replacement**: To prevent partial or corrupted file writes (e.g., during sudden client power-down), data is serialized first to a temporary file (`.tmp`). Once written successfully, the original file is overwritten using atomic file operations.
3. **Backup on Overwrite**: Before replacing the main data file, the previous version of the file is backed up to a `.bak` copy (e.g., `games.json.bak`). This guarantees that a valid state is always retained.
4. **Corruption Recovery**: If loading the primary `.json` file fails due to format corruption or file-system read errors, the repository automatically falls back to reading the `.bak` file, logs the warning, and restores the corrupted file from the backup.

---

## 3. Future Server Synchronization
The local game library is designed with remote synchronization in mind, preparing for full cloud/server integration:
- **Change Tracking**: Timestamps (`CreatedAt`, `UpdatedAt`) are tracked on every entity to support delta-sync protocols, avoiding expensive full-table transfers.
- **Source Differentiation**: The `Source` field distinguishes locally-added games (`Manual` or `Scanner`) from centralized configurations (`Server`).
- **Conflict Resolution**:
  - `Server`-sourced configurations are strictly read-only on the client side and always overridden by updates from the Sayra Server.
  - `Manual` configurations can be optionally uploaded to the server to synchronize the client state across multiple gaming booths.
- **Synchronization Hub**: In a future phase, a `SyncManager` will integrate with the existing TCP/IPC transport layer to receive push updates from the central administration dashboard and push local discovery updates.

---

## 4. Security Considerations
Managing local game executions requires rigorous security boundaries:
1. **Executable Path Validation**: The `ValidateGamePath` method ensures that paths are restricted to existing files on disk, preventing arbitrary path injections or invalid execution errors.
2. **Input Sanitization**: Arguments, working directories, and environment variables are strictly sanitized prior to persistence, mitigating potential shell injection vectors when processes are eventually spawned.
3. **Access Controls**: The data files stored in `Data/GameLibrary/` should be protected with Windows Access Control Lists (ACLs), ensuring that only the authorized Sayra Client service (or elevated system administrators) can read or write to the configurations.
4. **Binary Signature Verification**: Future iterations of the system will integrate process whitelisting and binary verification (e.g., Authenticode checking) to ensure only untampered, genuine executables can be added or executed.
