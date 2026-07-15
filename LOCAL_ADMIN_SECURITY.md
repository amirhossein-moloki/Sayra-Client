# Sayra Client: Local Administration Security & Configuration

This document outlines the security architecture, data handling practices, and integration models implemented for the **Client Local Administration** module (`Sayra.Client.LocalAdmin`).

---

## 1. Authentication Flow

The local administrator authentication is fully self-contained, requiring zero external server dependencies, ensuring offline administration capability for Game Center staff during internet or network outages.

```
+---------------+           +--------------------+           +----------------------+
|   Admin UI    |           | LocalAdminService  |           | LocalAdminRepository |
+---------------+           +--------------------+           +----------------------+
        |                             |                                 |
        |--- Authenticate(u, p) ----->|                                 |
        |                             |--- LoadCredentials() ---------->|
        |                             |<-- List<Credentials> -----------|
        |                             |                                 |
        |                             |-- [Validate Lockout State]      |
        |                             |-- [Verify Password PBKDF2]      |
        |                             |                                 |
        |                             |--- SaveCredentials() (If Fail)-->|
        |                             |                                 |
        |                             |-- [Create In-Memory Session]    |
        |<-- AuthResult (Token) ------|                                 |
        |                             |                                 |
```

### Steps:
1. **User Request**: The future Settings/Admin UI captures the username and password from the staff.
2. **Lockout Check**: The `LocalAdminService` verifies if the targeted account is currently locked.
   - If locked, and the lockout period (5 minutes) has not elapsed, authentication fails immediately.
   - If locked, but the lockout period has expired, the lockout state is automatically cleared.
3. **Password Verification**: The service retrieves the unique cryptographic salt and the stored PBKDF2 hash, then computes the hash of the supplied password using the constant-time string comparison API (`CryptographicOperations.FixedTimeEquals`).
4. **Session Generation**: Upon successful verification, the `AdminSessionManager` creates an in-memory session with a 15-minute sliding inactivity timeout and returns a secure, randomly generated Base64 session token.
5. **Lockout Escalation**: On authentication failure, the failed attempts counter is incremented. If it reaches **5 consecutive failed attempts**, the account is locked for **5 minutes**.

---

## 2. Password Hashing Strategy

To protect administrator credentials against brute-force and offline dictionary attacks, the module employs industry-standard password derivation practices:

* **Algorithm**: PBKDF2 (Password-Based Key Derivation Function 2) with a **SHA256** pseudorandom function.
* **Work Factor**: **350,000 iterations**, striking an optimal balance between server-grade security and client-side performance.
* **Salt**: A unique, cryptographically strong random salt (**32 bytes / 256 bits**) generated per administrator using `RandomNumberGenerator.GetBytes`.
* **Hash Output**: **32 bytes (256 bits)**, stored as a Base64-encoded string.
* **Timing Attack Mitigation**: Comparisons are executed in constant-time using `System.Security.Cryptography.CryptographicOperations.FixedTimeEquals` to prevent side-channel timing analysis.

### Password Complexity Rules:
All passwords must pass a strict local strength validator (`ValidatePasswordStrength`) which enforces:
* Minimum length of **8 characters**.
* At least **1 uppercase letter** (`A-Z`).
* At least **1 lowercase letter** (`a-z`).
* At least **1 digit** (`0-9`).
* At least **1 special character** (e.g., `!`, `@`, `#`, `$`, `%`, etc.).

---

## 3. Storage Format & Integrity Protection

All security credentials and configuration files are persisted locally using secure file-handling mechanisms designed for the high-frequency restart environments of cyber cafes.

### File Paths:
* **Admin Credentials**: `Data/LocalAdmin/admin_credentials.json`
* **Configuration Settings**: `Data/Configuration/client_config.json`

### File Handling & Atomic Writes:
To prevent data loss or file corruption during power failures or hard client system restarts:
1. **Asynchronous Serialization**: Data is serialized to memory and written asynchronously to a temporary file (`.tmp`).
2. **Automatic Backups**: The existing file is duplicated as a `.bak` backup file before replacement.
3. **Atomic Swap**: The `.tmp` file is moved to replace the main file.
4. **Corruption Recovery**: If the main JSON file becomes corrupted or fails to load, the storage repository detects the failure, rolls back to the `.bak` file, and overwrites the main file with the restored healthy backup.

### JSON Formats:

#### `admin_credentials.json` Example:
```json
[
  {
    "Id": "6b29fc40-1a1a-45c1-8438-fb1a3ea3f605",
    "Username": "admin",
    "PasswordHash": "OQG8Xn7yqZ...[Base64]...",
    "Salt": "rP9X8C...[Base64]...",
    "CreatedAt": "2023-11-20T10:00:00Z",
    "UpdatedAt": "2023-11-20T10:05:00Z",
    "LastLoginAt": "2023-11-20T10:05:00Z",
    "FailedAttempts": 0,
    "IsLocked": false,
    "LockedUntil": null
  }
]
```

#### `client_config.json` Example:
```json
{
  "ServerDiscovery": {
    "ServerIp": "127.0.0.1",
    "UdpPort": 37020,
    "AutoDiscovery": true
  },
  "GameLibrary": {
    "LibraryPath": "C:\\Program Files\\Sayra\\Games",
    "AutoUpdate": true
  },
  "ScannerPaths": [
    "C:\\Program Files (x86)\\Steam\\steamapps\\common",
    "D:\\Games"
  ],
  "LocalPreferences": {
    "Theme": "Dark",
    "Language": "fa-IR",
    "IsKioskMode": true
  }
}
```

---

## 4. Security Limitations & Hardening

* **No Plaintext Storage**: Passwords are never written, cached, or transferred in plaintext.
* **Local Scope Only**: Local admin credentials reside purely within the individual PC storage, ensuring an exploit on one PC does not compromise the administrative credentials of another PC.
* **Memory Protection**: Session tokens are maintained purely in-memory in thread-safe concurrent dictionaries and are never written to disk.
* **File System Permissions**: In a production environment, the `C:\Program Files\Sayra\Data` folder must be locked down using Windows ACLs to allow read/write access only to the SYSTEM and local Administrators groups, preventing standard Windows users (Gamers) from altering configuration or accessing backup hashes.

---

## 5. Future UI Integration

This backend-first administration system exposes fully async and clean APIs that can be seamlessly consumed by future presentation components:

1. **Service Registration**:
   The module exposes `AddLocalAdmin()` which hooks all security and storage dependencies into the client container.
2. **Accessing Configuration**:
   ```csharp
   // In WPF ViewModels / Controllers
   var config = await _clientConfigurationService.GetConfigurationAsync();
   ```
3. **Verifying Settings Changes**:
   To change critical settings, the UI can request authentication or check if there is an active session token:
   ```csharp
   bool isAuthorized = _adminSessionManager.ValidateSession(sessionToken);
   ```
