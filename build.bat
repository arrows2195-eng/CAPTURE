@echo off
echo ================================
echo    Screen 1 - Build Script
echo ================================
echo.

where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: .NET SDK not found!
    echo Download from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Building...
dotnet build -c Release

if %ERRORLEVEL% neq 0 (
    echo.
    echo BUILD FAILED!
    echo.
    echo Make sure you have:
    echo  - .NET SDK installed
    echo  - .NET Framework 4.8 targeting pack
    pause
    exit /b 1
)

echo.
echo BUILD SUCCESS!
echo.
echo Output: bin\Release\net48\Screen1.exe
echo.

if exist "bin\Release\net48\Screen1.exe" (
    set /p RUN="Run now? (Y/N): "
    if /i "%RUN%"=="Y" start "" "bin\Release\net48\Screen1.exe"
)

pause
