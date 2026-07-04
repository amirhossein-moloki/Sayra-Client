# Sayra Client - Installation Guide

## System Requirements
- Windows 10/11 or Windows Server 2019+
- .NET 8 Runtime (included in self-contained build)
- Administrator privileges

## Installation Steps
1. **Download the Release:** Obtain the `publish.zip` from the release portal.
2. **Extract:** Extract the contents to a temporary folder.
3. **Run Installer:** Open PowerShell as Administrator and run:
   ```powershell
   .\install.ps1
   ```

*Note: For commercial deployments requiring MSI or EXE installers, please use the Advanced Installer project or WiX Toolset scripts provided in the `tools/installer` directory (Enterprise License required).*
4. **Configuration:** Modify `C:\Program Files\SayraClient\appsettings.json` to point to your server IP.
5. **Restart Service:**
   ```powershell
   Restart-Service SayraClient
   ```

## Verification
Check `C:\Program Files\SayraClient\logs\` to ensure the client is connecting to the server.
