# SAYRA Enterprise Windows Client
# Phase 8 — Stage 10 Security Audit Report

## 1. Security & Data Protection Policies

Telemetry and distributed tracing platforms operate at a privileged level. To guarantee absolute compliance with data privacy regulations, the SAYRA Observability Platform implements multi-layered security controls.

---

## 2. Telemetry and Tracing Sanitization

- **Objective:** Ensure that telemetry records and trace scopes never leak sensitive user data such as passwords, access tokens, private keys, or secrets.
- **Sanitization Strategy:**
  1. Property keys, tag keys, and operation strings are programmatically validated against a hard blocklist of sensitive sub-strings (e.g., `password`, `token`, `secret`, `private_key`, `apikey`, `pwd`).
  2. The `ObservabilityStage10Tests.SecurityAudit_TelemetryAndTracing_NeverContainsCredentialsOrSecrets` test programmatically verifies that these forbidden keys are absent from all generated records.
  3. Any telemetry collection that involves user-session tracking (such as `WindowsSessionsCollector`) strictly collects generic metrics like Session ID, Idle Time, or Login Counts, completely bypassing authentication inputs or sensitive credential buffers.

---

## 3. Cryptographic Storage-at-Rest

- **Objective:** Secure long-term historical metrics, performance logs, and audit logs stored on disk against physical tampering or off-line database hijacking.
- **Implementation:**
  1. The local SQLite database utilizes the enterprise **SQLCipher engine-level encryption provider** (`Microsoft.Data.Sqlite.Core` with `SQLitePCLRaw.bundle_e_sqlcipher`).
  2. The database encryption master key (256-bit) is generated on first-run, enveloped using standard Windows DPAPI (using the centralized `ICryptographyService`), and saved at-rest in `Data/db_key.bin`.
  3. Plaintext master key bytes are loaded directly into pinned unmanaged memory (reusing the centralized `SessionKeyProvider` / `SecureMemoryBuffer`) with zero GC-managed exposure, and cleared immediately after connection handshake.
  4. Adversarial tests (`SecurityTests.SqlCipher_EncryptionAtRest_VerifyTamperingAndLockdown`) confirm that any standard, unencrypted attempt to access the database fails-closed with severe encryption violations.

---

## 4. Transport and Communication Security

- **Objective:** Protect inter-process communication (IPC) and client-server metrics transmission against sniffing, replay attacks, and man-in-the-middle hijacking.
- **Implementation:**
  1. **Secure Named Pipes IPC:** Named Pipes DACLs explicitly restrict connections to `SYSTEM`, `Administrators`, and the current workstation interactive user SID (`InteractiveSid`), blocking broad authenticated network users. Message sizes are strictly capped at 64KB, and messages undergo time-skew and cryptographic replay-protection audits.
  2. **Tls Transport & Pinning:** Secure connections utilize the `TlsConnectionManager` which strictly enforces native TLS 1.3 via `SocketsHttpHandler` in the DI container. All connections bypass the local trust store and execute certificate pinning validation against public key hashes or certificate thumbprints, preventing MITM proxies from intercepting metrics data.
