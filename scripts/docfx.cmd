@echo off
REM DocFX batch wrapper for Windows CMD users
REM This script runs the PowerShell DocFX script

setlocal enabledelayedexpansion

REM Check if PowerShell is available
where powershell >nul 2>&1
if errorlevel 1 (
    echo Error: PowerShell is not available or not in PATH
    echo Please install PowerShell or use PowerShell directly
    exit /b 1
)

REM Get the directory of this batch file
set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%docfx.ps1"

REM Check if the PowerShell script exists
if not exist "%PS_SCRIPT%" (
    echo Error: docfx.ps1 not found in %SCRIPT_DIR%
    exit /b 1
)

echo Running DocFX via PowerShell...
echo.

REM Run the PowerShell script with all arguments
if "%*"=="" (
    REM No arguments provided, run with defaults
    powershell -ExecutionPolicy Bypass -File "%PS_SCRIPT%"
) else (
    REM Pass all arguments to PowerShell script
    powershell -ExecutionPolicy Bypass -File "%PS_SCRIPT%" %*
)

REM Preserve exit code
exit /b %ERRORLEVEL%
