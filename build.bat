@echo off
setlocal
set "PATH=%USERPROFILE%\.dotnet;%PATH%"
echo ===================================================
echo Building SelectAI - Windows 11 AI Selection Tool
echo ===================================================

dotnet build SelectAI.sln -c Release
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Running Automated Test Suite...
dotnet test SelectAI.Tests\SelectAI.Tests.csproj
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Tests failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Publishing Application...
dotnet publish SelectAI\SelectAI.csproj -c Release -r win-x64 --self-contained false -o .\publish
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Publish failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ===================================================
echo SelectAI Built Successfully!
echo Executable located at: publish\SelectAI.exe
echo Default Global Hotkey: Ctrl + Shift + Space
echo ===================================================
pause
