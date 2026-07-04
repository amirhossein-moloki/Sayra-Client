# Sayra Client - Deployment Guide

## Preparation
- Ensure `SAYRA_MASTER_KEY` environment variable is set on target machines or configured in `appsettings.json`.
- Firewall: Allow outbound TCP traffic on the configured port (default: 5000).

## Bulk Deployment
For deploying to multiple PCs in a cyber cafe:
1. Use a deployment tool like PDQ Deploy, SCCM, or a simple GRP script.
2. Ensure the `publish/` directory is accessible on the network.
3. Execute `install.ps1` silently:
   ```powershell
   powershell.exe -ExecutionPolicy Bypass -File .\install.ps1
   ```

## Security Best Practices
- Change the `MasterKey` before production deployment.
- Ensure the `logs` directory is monitored for unauthorized access.
- Use Kiosk Mode to prevent users from bypassing the client.
