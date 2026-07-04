# Sayra Client Installation Script
# Requires Administrator Privileges

$ServiceName = "SayraClient"
$ServiceDisplayName = "Sayra Client Service"
$InstallDir = "C:\Program Files\SayraClient"
$BinaryPath = "$InstallDir\SayraClient.exe"

# 1. Verify Admin Privileges
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as an Administrator."
    exit 1
}

try {
    # 2. Create Directories
    Write-Host "Creating installation directories..." -ForegroundColor Cyan
    if (-not (Test-Path $InstallDir)) { New-Item -ItemType Directory -Path $InstallDir -ErrorAction Stop }
    if (-not (Test-Path "$InstallDir\logs")) { New-Item -ItemType Directory -Path "$InstallDir\logs" -ErrorAction Stop }

    # 3. Copy Files (Assumes files are in current directory)
    Write-Host "Copying files..." -ForegroundColor Cyan
    if (-not (Test-Path ".\publish")) {
        Write-Error "Publish directory not found. Run publish.ps1 first."
        exit 1
    }
    Copy-Item ".\publish\*" $InstallDir -Recurse -Force -ErrorAction Stop

    # 4. Register Service
    Write-Host "Registering Windows Service..." -ForegroundColor Cyan
    if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
        Write-Host "Stopping and removing existing service..."
        Stop-Service $ServiceName -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
    }

    New-Service -Name $ServiceName `
                -BinaryPathName "`"$BinaryPath`" --contentRoot `"$InstallDir`"" `
                -DisplayName $ServiceDisplayName `
                -Description "Sayra LAN Cyber Cafe Management Client Agent" `
                -StartupType Automatic -ErrorAction Stop

    # 5. Configure Recovery Options
    Write-Host "Configuring service recovery options..." -ForegroundColor Cyan
    sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

    # 6. Start Service
    Write-Host "Starting service..." -ForegroundColor Cyan
    Start-Service $ServiceName -ErrorAction Stop

    Write-Host "Installation completed successfully." -ForegroundColor Green
}
catch {
    Write-Host "Installation failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Attempting rollback..." -ForegroundColor Yellow

    if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
        Stop-Service $ServiceName -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
    }
    # Note: In a real commercial installer, we'd restore files from a backup.

    Write-Host "Rollback completed. Please check the error message above." -ForegroundColor White
    exit 1
}
