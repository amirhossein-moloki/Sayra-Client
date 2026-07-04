# Sayra Client Production Publish Script

$ProjectDir = "SayraClient"
$OutputDir = "publish"

Write-Host "Cleaning old publish directory..." -ForegroundColor Cyan
if (Test-Path $OutputDir) { Remove-Item -Recurse -Force $OutputDir }

Write-Host "Publishing Sayra Client for Windows (x64)..." -ForegroundColor Cyan
dotnet publish $ProjectDir -c Release -r win-x64 --self-contained true `
    /p:PublishSingleFile=true `
    /p:PublishReadyToRun=true `
    /p:PublishTrimmed=false `
    -o $OutputDir

Write-Host "Publish complete. Artifacts are in '$OutputDir' directory." -ForegroundColor Green
