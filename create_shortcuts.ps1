$wsh = New-Object -ComObject WScript.Shell

$targetDir = "$env:LOCALAPPDATA\Programs\SelectAI"
if (-not (Test-Path (Join-Path $targetDir "SelectAI.exe"))) {
    $targetDir = "C:\Users\bhara\Downloads\SelectAI-v1.1.0-win-x64"
}
$targetExe = Join-Path $targetDir "SelectAI.exe"

# 1. Desktop Shortcut
$desktop = [Environment]::GetFolderPath('Desktop')
$desktopLnk = Join-Path $desktop "SelectAI.lnk"
$shortcut = $wsh.CreateShortcut($desktopLnk)
$shortcut.TargetPath = $targetExe
$shortcut.WorkingDirectory = $targetDir
$shortcut.Description = "SelectAI - Galaxy AI Screen Selection and Assistant"
$shortcut.Save()

# 2. Start Menu Programs Shortcut
$programs = [Environment]::GetFolderPath('Programs')
$startLnk = Join-Path $programs "SelectAI.lnk"
$startShortcut = $wsh.CreateShortcut($startLnk)
$startShortcut.TargetPath = $targetExe
$startShortcut.WorkingDirectory = $targetDir
$startShortcut.Description = "SelectAI - Galaxy AI Screen Selection and Assistant"
$startShortcut.Save()

Write-Host "Created Desktop Shortcut at: $desktopLnk"
Write-Host "Created Start Menu Shortcut at: $startLnk"
