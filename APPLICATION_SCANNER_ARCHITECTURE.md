# Sayra Client: Application & Game Scanner Architecture

This document provides a detailed overview of the design, pipeline, capabilities, and performance optimizations of the **Sayra Client Application & Game Scanner Engine** (`Sayra.Client.Scanner`).

---

## 1. High-Level Architecture

The Scanner is a standalone, reusable backend class library targeting `.NET 8`. It automatically discovers, verifies, and extracts metadata from executables and shortcuts across a Windows system (while maintaining platform-agnostic safety).

### Folder Structure
```
Sayra.Client.Scanner/
├── Models/
│   ├── DetectedApplication.cs      # Final scanner output model
│   └── KnownGameSignature.cs      # Metadata definition for known games
├── ScannerEngine/
│   ├── GameDetectionEngine.cs     # Classification logic (Game vs App vs Launcher vs Ignore)
│   └── KnownGameDatabase.cs       # Database storing signature structures and utility blacklists
├── Providers/
│   ├── ExecutableMetadataProvider.cs # Extracts PE header metadata, hashes, and size
│   └── ShortcutParser.cs          # Parses Windows .lnk and .url files cross-platform
├── Validation/
│   └── ScannerValidator.cs        # Verifies readability, duplicates, and PE headers
├── Cache/
│   └── ScanCacheService.cs        # JSON cache storing validated items to avoid rescanning
└── Services/
    └── ApplicationScannerService.cs # Main orchestrator coordinating parallel scan pipeline
```

---

## 2. Detection & Classification Pipeline

The scanning process is executed in an asynchronous, non-blocking pipeline:

```
[Scan Request]
      │
      ▼
[Paths Discovery] (Launcher libraries, Start Menu, Desktop, Program Files, Custom paths)
      │
      ▼
[File Filtering] (Filter for .exe, .lnk, .url files; Ignore excluded systems like Windows/Temp)
      │
      ▼
[Shortcut Resolution] (Resolve targets for .lnk and .url files)
      │
      ▼
[Validation Phase] (Ensure file readability, exists, not blacklisted, and has a valid PE "MZ" header)
      │
      ▼
[Cache Lookup] (Verify if Size + Last Write Time match cache; return cached result on Hit)
      │
      ▼
[Metadata Extraction] (Extract Product Name, Version, Company, Size, SHA256, and Icons)
      │
      ▼
[Classification Engine] (Determine game vs app using Known Database & heuristics)
      │
      ▼
[Cache Persistence] (Save new entries to Data/Scanner/scan_cache.json)
      │
      ▼
[Game Library Registration] (Automatically add newly discovered games to IGameLibraryService with Source=Scanner)
```

---

## 3. Supported Launchers

The scanner supports automatic discovery of major digital gaming platforms:

1. **Steam**: Discovered via Windows registry keys (`WOW6432Node\Valve\Steam` or current user `SteamPath`) and standard common directory fallbacks.
2. **Epic Games**: Parses Epic `.item` manifests under `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests` to find precise installation directories and executables.
3. **Riot Games**: Parses `%PROGRAMDATA%\Riot Games\RiotClientInstalls.json` configuration file to resolve Riot launchers and paths.
4. **Ubisoft Connect**: Discovered via registry key `InstallDir` values and custom common game subdirectories.
5. **EA App & Origin**: Discovered via standard program folders and registries.
6. **Battle.net**: Discovered via default program directories.
7. **GOG Galaxy**: Discovered via GOG registry client paths.
8. **Xbox App (UWP)**: Discovered through `%ProgramFiles%\WindowsApps`.

---

## 4. Cache & Change Detection Strategy

To guarantee that scans are extremely fast after the first execution, an offline-first caching strategy is implemented in `ScanCacheService`:
- Cache data is stored in `Data/Scanner/scan_cache.json`.
- Changes are detected instantaneously using three high-speed properties:
  1. **File Size (bytes)**
  2. **Last Write Time (UTC Timestamp)**
  3. **File Hash (SHA256)**
- If the file size and last write time match, the scanner completely skips PE header extraction, hashing, and classification, saving extensive I/O overhead.

---

## 5. Performance Heuristics

1. **Fully Asynchronous and Parallel**: Scans are executed using `Parallel.ForEachAsync` with a controlled maximum degree of parallelism (`Environment.ProcessorCount`) to utilize all CPU cores without choking.
2. **Directory Exclusion List**: Heavy non-user folders (e.g., `Windows`, `$Recycle.Bin`, `System Volume Information`, `node_modules`, `obj`, `bin`, `AppData`) are completely skipped during traversal to reduce search times from minutes to seconds.
3. **PE Header Validation**: The scanner reads only the first 2 bytes of any `.exe` to verify the `MZ` magic header signature before initiating costly metadata parsing. This avoids processing corrupted files or non-executable assets.
4. **CancellationToken Support**: The entire pipeline respects `CancellationToken` to allow graceful cancellation by administrators at any stage without leaving corrupted cache files.

---

## 6. Future Extensibility

- **Online Signature Syncing**: The `KnownGameDatabase` can be connected to a remote REST API to update signatures dynamically.
- **Icon Extraction Enhancements**: On Windows, direct Windows API/COM icon extraction can be wrapped to output actual base64 image data.
- **Deep PE Parsing**: Advanced PE parsers can be integrated to read nested manifest resources directly from within `ExecutableMetadataProvider` if publishers omit standard FileVersionInfo blocks.
