# SelectAI Windows 11 Official App Installer
$ErrorActionPreference = "Stop"

$sourceDir = "d:\AI  select\dist\SelectAI-SelfContained"
$installDir = "$env:LOCALAPPDATA\Programs\SelectAI"
$exePath = Join-Path $installDir "SelectAI.exe"
$icoPath = Join-Path $installDir "Assets\app_icon.ico"
$uninstallBat = Join-Path $installDir "Uninstall.bat"

Write-Host "========================================="
Write-Host "Installing SelectAI into Windows Apps..."
Write-Host "Destination: $installDir"
Write-Host "========================================="

# 1. Stop any currently running instance
Get-Process SelectAI* -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 2. Create install directory
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# 3. Copy application files
Copy-Item -Path "$sourceDir\*" -Destination $installDir -Recurse -Force
Write-Host "[OK] Copied binaries and assets to $installDir"

# 4. Create Desktop Shortcut
$wsh = New-Object -ComObject WScript.Shell
$desktop = [Environment]::GetFolderPath('Desktop')
$desktopLnk = Join-Path $desktop "SelectAI.lnk"
$shortcut = $wsh.CreateShortcut($desktopLnk)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "SelectAI - Galaxy AI Screen Selection & Assistant"
$shortcut.IconLocation = "$icoPath,0"
$shortcut.Save()
Write-Host "[OK] Created Desktop Shortcut: $desktopLnk"

# 5. Create Start Menu Shortcut (Shows in Windows Start Menu & Search)
$programs = [Environment]::GetFolderPath('Programs')
$startLnk = Join-Path $programs "SelectAI.lnk"
$startShortcut = $wsh.CreateShortcut($startLnk)
$startShortcut.TargetPath = $exePath
$startShortcut.WorkingDirectory = $installDir
$startShortcut.Description = "SelectAI - Galaxy AI Screen Selection & Assistant"
$startShortcut.IconLocation = "$icoPath,0"
$startShortcut.Save()
Write-Host "[OK] Created Start Menu Shortcut: $startLnk"

# 6. Create Uninstall.bat
$uninstallContent = @"
@echo off
title Uninstalling SelectAI...
echo Stopping SelectAI...
taskkill /f /im SelectAI.exe 2>nul
echo Removing Shortcuts...
del /f /q "%USERPROFILE%\Desktop\SelectAI.lnk" 2>nul
del /f /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\SelectAI.lnk" 2>nul
echo Removing Registry Entries...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\SelectAI" /f 2>nul
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\App Paths\SelectAI.exe" /f 2>nul
echo Cleaning up files...
timeout /t 1 /nobreak >nul
rmdir /s /q "$installDir" 2>nul
echo SelectAI has been uninstalled.
pause
"@
[System.IO.File]::WriteAllText($uninstallBat, $uninstallContent)

# 7. Register in Windows "Installed apps" (Settings > Apps > Installed apps)
$uninstallRegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SelectAI"
if (-not (Test-Path $uninstallRegPath)) {
    New-Item -Path $uninstallRegPath -Force | Out-Null
}

Set-ItemProperty -Path $uninstallRegPath -Name "DisplayName" -Value "SelectAI" -Force
Set-ItemProperty -Path $uninstallRegPath -Name "DisplayVersion" -Value "1.0.0" -Force
Set-ItemProperty -Path $uninstallRegPath -Name "Publisher" -Value "SelectAI Open Source" -Force
Set-ItemProperty -Path $uninstallRegPath -Name "DisplayIcon" -Value "$icoPath,0" -Force
Set-ItemProperty -Path $uninstallRegPath -Name "InstallLocation" -Value $installDir -Force
Set-ItemProperty -Path $uninstallRegPath -Name "UninstallString" -Value "`"$uninstallBat`"" -Force
Set-ItemProperty -Path $uninstallRegPath -Name "URLInfoAbout" -Value "https://select-ai-bay.vercel.app" -Force
Set-ItemProperty -Path $uninstallRegPath -Name "HelpLink" -Value "https://github.com/bharath-0814/SelectAI" -Force
Set-ItemProperty -Path $uninstallRegPath -Name "EstimatedSize" -Value 180000 -Type DWord -Force
Set-ItemProperty -Path $uninstallRegPath -Name "NoModify" -Value 1 -Type DWord -Force
Set-ItemProperty -Path $uninstallRegPath -Name "NoRepair" -Value 1 -Type DWord -Force
Write-Host "[OK] Registered SelectAI in Windows Installed Apps"

# 8. Register in Windows App Paths (Win+R > SelectAI)
$appPathsReg = "HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\SelectAI.exe"
if (-not (Test-Path $appPathsReg)) {
    New-Item -Path $appPathsReg -Force | Out-Null
}
Set-ItemProperty -Path $appPathsReg -Name "(Default)" -Value $exePath -Force
Set-ItemProperty -Path $appPathsReg -Name "Path" -Value $installDir -Force
Write-Host "[OK] Registered SelectAI in Windows App Paths"

# 9. Register certificate in CurrentUser trust store
$cerPath = Join-Path $installDir "SelectAI_Certificate.cer"
if (Test-Path $cerPath) {
    certutil -user -addstore "TrustedPublisher" $cerPath | Out-Null
    certutil -user -addstore "TrustedPeople" $cerPath | Out-Null
    Write-Host "[OK] Registered Authenticode Certificate"
}

# 10. Also sync to Downloads folder for distribution
$downloadPkg = "C:\Users\bhara\Downloads\SelectAI-v1.0.0-win-x64"
if (-not (Test-Path $downloadPkg)) { New-Item -ItemType Directory -Path $downloadPkg -Force | Out-Null }
Copy-Item -Path "$sourceDir\*" -Destination $downloadPkg -Recurse -Force
Copy-Item -Path $uninstallBat -Destination $downloadPkg -Force

Write-Host "========================================="
Write-Host "SelectAI Installed Successfully!"
Write-Host "Launching SelectAI on desktop..."
Start-Process "explorer.exe" -ArgumentList "`"$exePath`""

# 11. Create updated distribution zip
$distZip = "d:\AI  select\dist\SelectAI-v1.0.0-win-x64.zip"
$userZip = "C:\Users\bhara\Downloads\SelectAI-v1.0.0-win-x64.zip"
if (Test-Path $distZip) { Remove-Item $distZip -Force }
Compress-Archive -Path "$sourceDir\*" -DestinationPath $distZip -CompressionLevel Optimal
Copy-Item $distZip $userZip -Force
Write-Host "[OK] Updated distribution ZIP packages"
