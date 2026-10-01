param()

$sourceDir = "d:\AI  select\dist\SelectAI-SelfContained"
$installDir = "$env:LOCALAPPDATA\Programs\SelectAI"
$downloadPkg = "C:\Users\bhara\Downloads\SelectAI-v1.0.0-win-x64"
$distZip = "d:\AI  select\dist\SelectAI-v1.0.0-win-x64.zip"
$userZip = "C:\Users\bhara\Downloads\SelectAI-v1.0.0-win-x64.zip"

Write-Host "Stopping any running SelectAI process..."
Get-Process SelectAI -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

Write-Host "Copying to installed program directory: $installDir"
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}
Copy-Item -Path "$sourceDir\*" -Destination $installDir -Recurse -Force
Write-Host "Installed files copied successfully."

Write-Host "Copying to Downloads folder: $downloadPkg"
if (-not (Test-Path $downloadPkg)) {
    New-Item -ItemType Directory -Path $downloadPkg -Force | Out-Null
}
Copy-Item -Path "$sourceDir\*" -Destination $downloadPkg -Recurse -Force

Write-Host "Creating distribution zip archive: $distZip"
if (Test-Path $distZip) {
    Remove-Item $distZip -Force
}
Compress-Archive -Path "$sourceDir\*" -DestinationPath $distZip -CompressionLevel Optimal
Write-Host "Distribution zip created."

Copy-Item $distZip $userZip -Force
Write-Host "User Downloads zip updated: $userZip"

Write-Host "=== Sync complete ==="
