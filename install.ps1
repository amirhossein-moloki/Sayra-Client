# Sayra Client Production Installation Script
# Standardized for Enterprise Deployment
# Target: C:\Program Files\Sayra

$ServiceName = "Sayra Client"
$ServiceDisplayName = "Sayra Client Service"
$BaseDir = "C:\Program Files\Sayra"
$CoreDir = "$BaseDir\CoreService"
$UIDir = "$BaseDir\UI"
$GamesDir = "$BaseDir\Games"
$ConfigDir = "$BaseDir\Config"
$LogsDir = "$BaseDir\Logs"
$UpdatesDir = "$BaseDir\Updates"

$BinaryPath = "$CoreDir\SayraClient.exe"

# 1. Verify Admin Privileges
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as an Administrator."
    exit 1
}

try {
    # 2. Create Standardized Directory Structure
    Write-Host "Creating installation directories at $BaseDir..." -ForegroundColor Cyan
    $Dirs = @($BaseDir, $CoreDir, $UIDir, $GamesDir, $ConfigDir, $LogsDir, $UpdatesDir)
    foreach ($dir in $Dirs) {
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -ErrorAction Stop | Out-Null }
    }

    # 3. Copy Binaries (Assumes files are in current directory /publish)
    Write-Host "Deploying binaries..." -ForegroundColor Cyan

    if (Test-Path ".\publish_core") {
        Copy-Item ".\publish_core\*" $CoreDir -Recurse -Force -ErrorAction Stop
    }

    if (Test-Path ".\publish_ui") {
        Copy-Item ".\publish_ui\*" $UIDir -Recurse -Force -ErrorAction Stop
    }

    # 4. Register and Configure Service
    Write-Host "Registering Windows Service..." -ForegroundColor Cyan
    if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
        Write-Host "Updating existing service..."
        Stop-Service $ServiceName -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
    }

    New-Service -Name $ServiceName `
                -BinaryPathName "`"$BinaryPath`" --contentRoot `"$CoreDir`"" `
                -DisplayName $ServiceDisplayName `
                -Description "Sayra LAN Kiosk & Session Management Service" `
                -StartupType Automatic -ErrorAction Stop

    # Enterprise Recovery Strategy:
    # Restart every 1 min on failure, reset fail count after 1 day.
    Write-Host "Configuring advanced recovery options..." -ForegroundColor Cyan
    sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000
    sc.exe config $ServiceName start= delayed-auto

    # 5. Firewall Rules
    Write-Host "Configuring firewall rules..." -ForegroundColor Cyan
    netsh advfirewall firewall add rule name="Sayra Core Service" dir=in action=allow protocol=TCP localport=5000-5010 profile=any | Out-Null

    # 6. Start Service
    Write-Host "Starting Sayra Core Service..." -ForegroundColor Cyan
    Start-Service $ServiceName -ErrorAction Stop

    Write-Host "Sayra Client installed successfully in $BaseDir" -ForegroundColor Green
}
catch {
    Write-Host "Installation failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Check logs for details." -ForegroundColor White
    exit 1
}
