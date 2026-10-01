@echo off
setlocal
set "PATH=%USERPROFILE%\.dotnet;%PATH%"

if exist "publish\SelectAI.exe" (
    echo Starting SelectAI from publish directory...
    start "" "publish\SelectAI.exe"
) else (
    echo Starting SelectAI via dotnet run...
    start "" dotnet run --project SelectAI\SelectAI.csproj
)
