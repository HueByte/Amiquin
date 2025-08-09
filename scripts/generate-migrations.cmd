@echo off
REM Batch file wrapper for generate-migrations.ps1
REM This allows running the migration script from CMD

if "%1"=="" (
    echo Error: Migration name is required.
    echo Usage: generate-migrations.cmd ^<MigrationName^>
    exit /b 1
)

REM Check if PowerShell is available
where pwsh >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    pwsh -ExecutionPolicy Bypass -File "%~dp0generate-migrations.ps1" -MigrationName "%1"
) else (
    where powershell >nul 2>nul
    if %ERRORLEVEL% EQU 0 (
        powershell -ExecutionPolicy Bypass -File "%~dp0generate-migrations.ps1" -MigrationName "%1"
    ) else (
        echo Error: PowerShell is not available on this system.
        exit /b 1
    )
)