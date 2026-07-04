# Sayra Client - Troubleshooting Guide

## Common Issues

### Client won't connect to server
- Check if `Sayra Server` is running.
- Verify IP/Port in `appsettings.json`.
- Check Firewall settings.
- Check logs for "Connection refused" or "Timeout" errors.

### Authentication Failed
- Ensure `MasterKey` matches on both Client and Server.
- Check if the system clock is synchronized (max 10s drift allowed for security).

### Kiosk Mode not working
- Ensure the client is running as `SYSTEM` or with Administrator privileges.
- Check if `AntiTamperService` is running.

### Service won't start
- Check Event Viewer -> Windows Logs -> Application.
- Ensure all dependencies are present in the installation folder.
