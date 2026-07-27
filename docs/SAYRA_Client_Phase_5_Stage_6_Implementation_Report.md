# SAYRA Enterprise Windows Client - Phase 5 Stage 6 Technical Report
## Enterprise Advertisement Platform & Client UI Integration

---

## 1. Executive Summary & Architecture

This document serves as the official enterprise implementation, technical specification, and verification report for **SAYRA Enterprise Windows Client Phase 5 — Stage 6 (Enterprise Advertisement Platform + Client UI Integration)**.

Stage 6 introduces a high-performance, fault-tolerant, and secure digital advertisement distribution and presentation engine into the SAYRA client architecture. It enables dynamic scheduling of image, video, and HTML campaigns, manages secure offline playback caches with strict LRU (Least-Recently-Used) quotas, ensures cryptographic signature/integrity verification, logs impressions (VIEW, CLICK, SKIP), and integrates beautifully with the SAYRA yellow-and-black branded user experience.

### Architectural Overview

The Advertisement Platform is designed using clean architecture patterns with decoupled responsibility separation:

```
┌────────────────────────────────────────────────────────────────────────┐
│                              SAYRA.UI                                  │
│   ┌─────────────────────────┐           ┌─────────────────────────┐    │
│   │   MaintenanceOverlay    │◄─────────►│       AdCarousel        │    │
│   └─────────────────────────┘           └─────────────────────────┘    │
└──────────────────────────────────────┬─────────────────────────────────┘
                                       │ Subscribes to Events
┌──────────────────────────────────────▼─────────────────────────────────┐
│                           SAYRA CLIENT CORE                            │
│   ┌────────────────────────────────────────────────────────────────┐   │
│   │                     AdvertisementEngine                        │   │
│   └───────┬────────────────────┬────────────────────┬──────────────┘   │
│           │                    │                    │                  │
│   ┌───────▼──────────┐ ┌───────▼──────────┐ ┌───────▼──────────┐       │
│   │CampaignScheduler │ │ AdDownloadManager│ │AdvertisementCache│       │
│   └──────────────────┘ └──────────────────┘ └──────────────────┘       │
│           │                                         │                  │
│   ┌───────▼─────────────────────────────────────────▼──────────────┐   │
│   │                    AdvertisementRepository                     │   │
│   └────────────────────────────────┬───────────────────────────────┘   │
│                                    │ SQLCipher Encrypted Writes        │
│   ┌────────────────────────────────▼───────────────────────────────┐   │
│   │                     Secure Local Database                      │   │
│   └────────────────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Implemented Services & Interfaces

All contracts are defined under the `Sayra.Client.Shared.Interfaces` namespace to permit seamless cross-module compilation and mock virtualization.

### 2.1 Services & Engine Components

1. **`IAdvertisementRepository` / `AdvertisementRepository`**:
   Acts as the central SQLCipher data access layer. Persists campaign schemas, downloaded media metadata, view/click/skip impressions, and playback histories. Utilizes strictly parameterized queries and atomic transaction scopes.
2. **`IAdDownloadManager` / `AdDownloadManager`**:
   Leverages `HttpClient` for background content delivery. Implements multi-attempt retry policies, HTTP Range request header parsing to support interrupted download resuming, SHA-256 media checksum verification, and file system size monitoring against configured quotas.
3. **`IAdvertisementCache` / `AdvertisementCache`**:
   Implements a robust Least-Recently-Used (LRU) cache eviction algorithm based on `LastAccessedAt` database timestamps. Enforces hard maximum storage boundaries, purges expired campaigns, and generates tamper-detection notifications.
4. **`ICampaignScheduler` / `CampaignScheduler`**:
   Determines campaign playability based on active dates, daily hour ranges (e.g. `["14:00-18:00"]` supporting overnight schedules), priority ranks (`Emergency > High > Medium > Low`), and conflict resolution rules. Returns a standard pre-packaged fallback advertisement when no scheduled ads are playable.
5. **`IAdvertisementEngine` / `AdvertisementEngine`**:
   The central orchestrating background worker. Handles incoming master campaign synchronizations, enforces RSA-SHA256 signature verifications, applies version codes to block downgrades, and triggers background download queues.
6. **`IMediaPlaybackService` / `MediaPlaybackService`**:
   Orchestrates playback transitions, logs history states (Completed, Failed, Skipped), handles play timeouts, and publishes decoupled status events to the UI thread.
7. **`IImpressionTracker` / `ImpressionTracker`**:
   Persists impression metrics locally to SQLite tables, prepared for batch uploads to the master server during subsequent telemetry synchronization intervals.

---

## 3. Database Schema (Migration Version 4)

Database migrations transition smoothly from Version 3 to Version 4. SQLCipher tables and performance indexes are defined as follows:

```sql
-- Campaigns Table
CREATE TABLE IF NOT EXISTS AdCampaigns (
    CampaignId TEXT PRIMARY KEY NOT NULL,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL,
    MediaUrl TEXT NOT NULL,
    MediaLocalPath TEXT NOT NULL,
    TargetUrl TEXT NOT NULL,
    Priority INTEGER DEFAULT 1,
    DisplayDurationSeconds INTEGER DEFAULT 10,
    StartTime TEXT NOT NULL,
    EndTime TEXT NOT NULL,
    DailyActiveHours TEXT NOT NULL,
    IsDownloaded INTEGER DEFAULT 0,
    Checksum TEXT NOT NULL,
    Signature TEXT NOT NULL,
    MediaSize INTEGER DEFAULT 0,
    VersionCode INTEGER DEFAULT 1
);

CREATE INDEX IF NOT EXISTS IDX_AdCampaigns_Timeline ON AdCampaigns (StartTime, EndTime, IsDownloaded);
CREATE INDEX IF NOT EXISTS IDX_AdCampaigns_Priority ON AdCampaigns (Priority);

-- Impressions Table (Local Buffering)
CREATE TABLE IF NOT EXISTS AdImpressions (
    ImpressionId TEXT PRIMARY KEY NOT NULL,
    CampaignId TEXT NOT NULL,
    SessionId TEXT,
    ImpressionType TEXT NOT NULL, -- VIEW, CLICK, SKIP
    PlaybackDurationSeconds REAL NOT NULL,
    CreatedAt TEXT NOT NULL,
    IsSynced INTEGER DEFAULT 0
);

CREATE INDEX IF NOT EXISTS IDX_AdImpressions_CampaignId ON AdImpressions (CampaignId);

-- Cached Download Tracking
CREATE TABLE IF NOT EXISTS DownloadedMedia (
    MediaPath TEXT PRIMARY KEY NOT NULL,
    CampaignId TEXT NOT NULL,
    FileSize INTEGER NOT NULL,
    LastAccessedAt TEXT NOT NULL,
    Checksum TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IDX_DownloadedMedia_CampaignId ON DownloadedMedia (CampaignId);

-- Playback History Table
CREATE TABLE IF NOT EXISTS PlaybackHistory (
    PlaybackId TEXT PRIMARY KEY NOT NULL,
    CampaignId TEXT NOT NULL,
    StartedAt TEXT NOT NULL,
    CompletedAt TEXT NOT NULL,
    DurationSeconds REAL NOT NULL,
    Status TEXT NOT NULL, -- COMPLETED, FAILED, SKIPPED
    ErrorMessage TEXT
);

CREATE INDEX IF NOT EXISTS IDX_PlaybackHistory_CampaignId ON PlaybackHistory (CampaignId);
```

---

## 4. Cache & Eviction Strategy (LRU Quotas)

To prevent client storage bloat, the **`AdvertisementCache`** actively tracks disk usage:
- **Configure limits**: Configurable max limit (default: 500MB).
- **Eviction algorithm**: Prior to executing a new background media download, `AdvertisementEngine` queries the cache size. If writing the incoming file would exceed the quota, `AdvertisementCache` sorts all stored downloads by their database `LastAccessedAt` timestamp. It systematically deletes files starting from the oldest until adequate space is cleared.
- **Audit tracking**: Deletions trigger serilog event tracking and secure appending to `AuditService` under `LogSecurity`.

---

## 5. Security & Validation Pipelines

Each campaign undergoes a strict validation pipeline before ingestion:

1. **Digital Signature Verification**: The engine uses `ISignatureVerifier` to cross-examine a SHA-256 payload digest against `server_public.key` using RSA. Any mismatch rejects the campaign.
2. **Anti-Downgrade Versioning**: Stored `VersionCode` values are tracked. Incoming requests with smaller version numbers are ignored.
3. **Expiration check**: Campaigns with end dates older than `DateTime.UtcNow` are dropped instantly.
4. **Integrity (Checksum) Verification**: Once downloaded, the media file's SHA-256 hash is calculated and matched against the metadata checksum. If a mismatch is found, the file is immediately quarantined and deleted.

---

## 6. Client UI Integration

The presentation layer incorporates the visual components:

- **`AdCarousel.xaml`**:
  An animated user control supporting `IMAGE`, `VIDEO` (using hardware-accelerated WPF `MediaElement`), and `HTML` (via `WebBrowser`). It handles fading storyboard transitions programmatically without locking the UI thread.
- **`MaintenanceOverlay.xaml`**:
  A fullscreen window styled in SAYRA's official yellow-and-black palette. It embeds `AdCarousel` to serve ads and display system status messages to users during lockouts, optimizing center revenue.

---

## 7. Test Coverage Summary

Comprehensive unit and integration testing has been completed in `Sayra.Client.Configuration.Tests/AdvertisementSystemTests.cs`. All tests pass sequential runs successfully:

| Test Case | Objective / Verification | Status |
| :--- | :--- | :--- |
| `Migration_4_Creates_Advertisement_Tables_And_Indexes` | Verifies Migration 4 schema changes apply safely inside SQLCipher. | **PASS** |
| `Repository_Save_And_Retrieve_Campaign_And_Impressions` | Assures CRUD and active-timeline queries are correctly parameterized. | **PASS** |
| `DownloadManager_Saves_Valid_File_And_Resumes_Interrupted_Download` | Verifies chunked download resumes with custom offset bytes. | **PASS** |
| `DownloadManager_Rejects_Corrupted_Media_Checksum_Mismatch` | Confirms corrupted downloads are discarded upon validation failures. | **PASS** |
| `Cache_LRU_Eviction_Under_Quota_Works_Properly` | Validates Least-Recently-Used file evictions against storage quotas. | **PASS** |
| `Scheduler_Selects_Highest_Priority_Active_And_Respects_Daily_Hours` | Assures priority-based overriding and daily hour timeframe checks. | **PASS** |
| `PlaybackService_Fires_Events_Successfully_On_Start_And_Complete` | Tests decoupled asynchronous event dispatching for UI consumers. | **PASS** |
| `ImpressionTracker_Saves_Impressions_And_Allows_Later_Sync_Query` | Validates impression storage, click logging, and synchronization queues. | **PASS** |

---

## 8. Known Limitations & Recommendations

- **WPF MediaElement Headless Restrictions**: WPF UI rendering and MediaElement audio/video features depend on native Windows APIs. These visual controls are excluded from CLI test projects to allow cross-platform testing (e.g. Linux container build hosts).
- **WebBrowser IE Engine Fallback**: The standard WPF `WebBrowser` uses the underlying MSHTML (IE) engine on older Windows systems. It is recommended to migrate to WebView2 (Chromium-based) in production for modern HTML5 support.
