# Sayra Client - Upgrade Guide

## Manual Upgrade
1. Stop the service: `Stop-Service SayraClient`
2. Replace files in `C:\Program Files\SayraClient\` with new version.
3. Start the service: `Start-Service SayraClient`

## Automatic Upgrade
The client includes an `UpdateManager` that polls for updates.
To trigger an update from the server:
1. Send an `UPDATE` command with `downloadUrl` and `checksum`.
2. The client will download the package and execute the update flow.

## Rollback
If an update fails:
1. The service recovery settings will attempt to restart the process.
2. If the binary is corrupted, manually restore from the last backup in the installation directory.
