#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate Entity Framework migrations for all supported database providers
.DESCRIPTION
    This script generates EF Core migrations for SQLite, MySQL, PostgreSQL, and MSSQL databases
.PARAMETER MigrationName
    The name of the migration to create
.EXAMPLE
    ./generate-migrations.ps1 AddUserTable
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$MigrationName,
    
    [switch]$Help
)

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Detailed
    exit 0
}

# Validate migration name
if ([string]::IsNullOrWhiteSpace($MigrationName)) {
    Write-Error "Error: Migration name is required."
    Write-Host "Usage: ./generate-migrations.ps1 <MigrationName>"
    exit 1
}

# Set the root directory
$RootDir = Split-Path -Parent $PSScriptRoot

# Define relative paths
$StartupProject = Join-Path $RootDir "source\Amiquin.Bot"
$InfrastructureProject = Join-Path $RootDir "source\Amiquin.Infrastructure"
$ContextName = "AmiquinContext"
$MigrationDir = Join-Path $RootDir "source\Migrations"

Write-Host "Root directory: $RootDir" -ForegroundColor Cyan
Write-Host "Migration name: $MigrationName" -ForegroundColor Cyan
Write-Host ""

# Set environment variable to skip database connection during migrations
$env:DOTNET_RUNNING_IN_CONTAINER = "true"

# Store original directory
$OriginalLocation = Get-Location

try {
    # Database modes with corrected mapping
    # Mode 0 = MySQL, Mode 1 = SQLite, Mode 2 = PostgreSQL, Mode 3 = MSSQL
    $DatabaseModes = @(
        @{ Mode = 1; Name = "SQLite"; ProjectSuffix = "Sqlite"; Provider = "Sqlite" },
        @{ Mode = 0; Name = "MySQL"; ProjectSuffix = "MySql"; Provider = $null },
        @{ Mode = 2; Name = "PostgreSQL"; ProjectSuffix = "Postgres"; Provider = "Npgsql" },
        @{ Mode = 3; Name = "MSSQL"; ProjectSuffix = "MSSql"; Provider = "SqlServer" }
    )

    foreach ($DbMode in $DatabaseModes) {
        $env:AMQ_DATABASE_MODE = $DbMode.Mode
        Write-Host "Running migration for $($DbMode.Name) (AMQ_DATABASE_MODE=$($DbMode.Mode))..." -ForegroundColor Green
        
        # Change to infrastructure project directory
        Set-Location $InfrastructureProject
        
        # Build the migration command
        $MigrationProject = Join-Path ".." "Migrations" "Amiquin.$($DbMode.ProjectSuffix)"
        $OutputDir = Join-Path $MigrationDir "Amiquin.$($DbMode.ProjectSuffix)" "Migrations"
        $MigrationNameWithSuffix = "${MigrationName}_$($DbMode.ProjectSuffix)"
        
        $Arguments = @(
            "ef", "migrations", "add", $MigrationNameWithSuffix,
            "--startup-project", $StartupProject,
            "--output-dir", $OutputDir,
            "--context", $ContextName,
            "--project", $MigrationProject
        )
        
        # Add provider argument if specified
        if ($DbMode.Provider) {
            $Arguments += "--"
            $Arguments += "--provider"
            $Arguments += $DbMode.Provider
        }
        
        Write-Host "Executing: dotnet $($Arguments -join ' ')" -ForegroundColor DarkGray
        
        # Execute the migration command
        $Process = Start-Process -FilePath "dotnet" -ArgumentList $Arguments -NoNewWindow -PassThru -Wait
        
        if ($Process.ExitCode -eq 0) {
            Write-Host "✓ Migration for $($DbMode.Name) created successfully" -ForegroundColor Green
        } else {
            Write-Warning "Migration for $($DbMode.Name) failed with exit code $($Process.ExitCode)"
        }
        
        Write-Host ""
    }
}
catch {
    Write-Error "An error occurred: $_"
    exit 1
}
finally {
    # Clean up environment variables
    Remove-Item Env:\AMQ_DATABASE_MODE -ErrorAction SilentlyContinue
    Remove-Item Env:\DOTNET_RUNNING_IN_CONTAINER -ErrorAction SilentlyContinue
    
    # Restore original directory
    Set-Location $OriginalLocation
    
    Write-Host "Environment variables have been removed." -ForegroundColor Yellow
    Write-Host "Migrations generation completed!" -ForegroundColor Cyan
}