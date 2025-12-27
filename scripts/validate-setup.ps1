#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validation script for Amiquin project setup and configuration
.DESCRIPTION
    Comprehensive validation script that checks:
    - Configuration file completeness
    - External dependencies
    - Environment setup
    - Service connectivity
    - Security configuration
#>

param(
    [switch]$Production,
    [switch]$Verbose,
    [switch]$Fix,
    [switch]$Help
)

if ($Help) {
    Write-Host "Amiquin Setup Validation Script" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "This script validates your Amiquin project setup by checking:"
    Write-Host "  - Configuration file completeness and validity"
    Write-Host "  - External service dependencies (Docker, ffmpeg, etc.)"
    Write-Host "  - Environment variable configuration"
    Write-Host "  - Service connectivity (Qdrant, MySQL, APIs)"
    Write-Host "  - Security configuration for production"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  ./validate-setup.ps1               # Basic validation"
    Write-Host "  ./validate-setup.ps1 -Production   # Production-level validation"
    Write-Host "  ./validate-setup.ps1 -Verbose      # Detailed output"
    Write-Host "  ./validate-setup.ps1 -Fix          # Attempt to fix issues automatically"
    Write-Host ""
    Write-Host "Parameters:"
    Write-Host "  -Production   Perform production-level security and performance checks"
    Write-Host "  -Verbose      Show detailed information about each check"
    Write-Host "  -Fix          Attempt to automatically fix detected issues"
    exit 0
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$SuccessCount = 0
$WarningCount = 0
$ErrorCount = 0

# Navigate to project root
Set-Location $ProjectRoot

Write-Host "=== Amiquin Setup Validation ===" -ForegroundColor Cyan
if ($Production) {
    Write-Host "Production Mode: Enhanced security and performance checks enabled" -ForegroundColor Yellow
}
Write-Host ""

# Validation functions
function Test-CheckResult {
    param(
        [string]$CheckName,
        [bool]$Success,
        [string]$Message = "",
        [string]$FixCommand = ""
    )
    
    if ($Success) {
        Write-Host "✓ $CheckName" -ForegroundColor Green
        $script:SuccessCount++
        if ($Verbose -and $Message) {
            Write-Host "  $Message" -ForegroundColor Gray
        }
    } else {
        if ($Message -match "WARNING|WARN") {
            Write-Host "⚠ $CheckName" -ForegroundColor Yellow
            $script:WarningCount++
        } else {
            Write-Host "❌ $CheckName" -ForegroundColor Red
            $script:ErrorCount++
        }
        
        if ($Message) {
            Write-Host "  $Message" -ForegroundColor Gray
        }
        
        if ($Fix -and $FixCommand) {
            Write-Host "  Attempting fix: $FixCommand" -ForegroundColor Cyan
            try {
                Invoke-Expression $FixCommand
                Write-Host "  ✓ Fix applied" -ForegroundColor Green
            } catch {
                Write-Host "  ❌ Fix failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
}

function Test-FileExists {
    param([string]$Path, [string]$Description)
    $exists = Test-Path $Path
    Test-CheckResult -CheckName "$Description exists" -Success $exists -Message "Path: $Path"
    return $exists
}

function Test-EnvironmentFile {
    Write-Host "Checking environment configuration..." -ForegroundColor Cyan
    
    # Check .env file exists
    $envExists = Test-FileExists -Path ".env" -Description ".env file"
    
    if ($envExists) {
        $envContent = Get-Content ".env" -ErrorAction SilentlyContinue
        
        # Required variables
        $requiredVars = @(
            @{Name="AMQ_Discord__Token"; Pattern="AMQ_Discord__Token="; Required=$true},
            @{Name="AMQ_LLM__Providers__OpenAI__ApiKey"; Pattern="AMQ_LLM__Providers__OpenAI__ApiKey="; Required=$true},
            @{Name="AMQ_Database__Mode"; Pattern="AMQ_Database__Mode="; Required=$true},
            @{Name="AMQ_Memory__Enabled"; Pattern="AMQ_Memory__Enabled="; Required=$false},
            @{Name="AMQ_Memory__Qdrant__Host"; Pattern="AMQ_Memory__Qdrant__Host="; Required=$false}
        )
        
        foreach ($var in $requiredVars) {
            $found = $envContent | Where-Object { $_ -match "^$($var.Pattern)" -and $_ -notmatch "your-.*-here" }
            $configured = $found -and ($found -split "=", 2)[1].Trim() -ne ""
            
            if ($var.Required) {
                Test-CheckResult -CheckName "$($var.Name) configured" -Success $configured -Message "Required for basic functionality"
            } else {
                if ($configured) {
                    Test-CheckResult -CheckName "$($var.Name) configured" -Success $true -Message "Optional feature enabled"
                } else {
                    Test-CheckResult -CheckName "$($var.Name) configured" -Success $false -Message "WARNING: Optional feature not configured"
                }
            }
        }
    }
    
    # Check appsettings.json
    $appSettingsPath = "source/Amiquin.Bot/Data/Configuration/appsettings.json"
    Test-FileExists -Path $appSettingsPath -Description "appsettings.json"
    
    Write-Host ""
}

function Test-ExternalDependencies {
    Write-Host "Checking external dependencies..." -ForegroundColor Cyan
    
    # Docker
    try {
        $null = docker --version 2>$null
        Test-CheckResult -CheckName "Docker installed" -Success $true
        
        try {
            $null = docker info 2>$null
            Test-CheckResult -CheckName "Docker running" -Success $true
        } catch {
            Test-CheckResult -CheckName "Docker running" -Success $false -Message "Docker daemon is not running"
        }
    } catch {
        Test-CheckResult -CheckName "Docker installed" -Success $false -Message "Docker is required for Qdrant and MySQL services"
    }
    
    # Docker Compose
    try {
        $null = docker-compose --version 2>$null
        Test-CheckResult -CheckName "Docker Compose installed" -Success $true
    } catch {
        Test-CheckResult -CheckName "Docker Compose installed" -Success $false -Message "Required for multi-service deployment"
    }
    
    # .NET SDK
    try {
        $dotnetVersion = dotnet --version 2>$null
        $versionMatch = $dotnetVersion -match "^9\."
        Test-CheckResult -CheckName ".NET 9.0 SDK installed" -Success $versionMatch -Message "Version: $dotnetVersion"
    } catch {
        Test-CheckResult -CheckName ".NET 9.0 SDK installed" -Success $false -Message "Required to build and run Amiquin"
    }
    
    # FFmpeg (optional but recommended)
    try {
        $null = ffmpeg -version 2>$null
        Test-CheckResult -CheckName "FFmpeg installed" -Success $true -Message "Required for voice features"
    } catch {
        Test-CheckResult -CheckName "FFmpeg installed" -Success $false -Message "WARNING: Voice features will not work without FFmpeg"
    }
    
    Write-Host ""
}

function Test-ServiceConnectivity {
    Write-Host "Checking service connectivity..." -ForegroundColor Cyan
    
    # Test Qdrant connection
    try {
        $qdrantResponse = Invoke-RestMethod -Uri "http://localhost:6333/health" -TimeoutSec 5 -ErrorAction Stop
        Test-CheckResult -CheckName "Qdrant service reachable" -Success $true
    } catch {
        Test-CheckResult -CheckName "Qdrant service reachable" -Success $false -Message "Start Qdrant with: docker-compose --profile qdrant-only up -d"
    }
    
    # Test MySQL connection (if configured)
    $envContent = Get-Content ".env" -ErrorAction SilentlyContinue
    $mysqlMode = $envContent | Where-Object { $_ -match "^AMQ_Database__Mode=0" }
    
    if ($mysqlMode) {
        try {
            $mysqlTest = docker exec mysql-amiquin-instance mysqladmin ping -h localhost 2>$null
            $mysqlRunning = $mysqlTest -match "mysqld is alive"
            Test-CheckResult -CheckName "MySQL service reachable" -Success $mysqlRunning
        } catch {
            Test-CheckResult -CheckName "MySQL service reachable" -Success $false -Message "Start MySQL with: docker-compose --profile database up -d"
        }
    } else {
        Test-CheckResult -CheckName "SQLite database mode" -Success $true -Message "No external database connection required"
    }
    
    Write-Host ""
}

function Test-SecurityConfiguration {
    if (-not $Production) {
        return
    }
    
    Write-Host "Checking security configuration (Production mode)..." -ForegroundColor Cyan
    
    $envContent = Get-Content ".env" -ErrorAction SilentlyContinue
    
    # Check for placeholder tokens
    $placeholderTokens = $envContent | Where-Object { 
        $_ -match "your-.*-here" -or 
        $_ -match "sk-test" -or 
        $_ -match "placeholder" 
    }
    
    Test-CheckResult -CheckName "No placeholder tokens in production" -Success ($placeholderTokens.Count -eq 0) -Message "Found $($placeholderTokens.Count) placeholder tokens"
    
    # Check for Qdrant authentication in production
    $qdrantAuth = $envContent | Where-Object { $_ -match "^AMQ_Memory__Qdrant__ApiKey=" -and $_ -notmatch "your-.*-here" }
    Test-CheckResult -CheckName "Qdrant authentication configured" -Success ($qdrantAuth -ne $null) -Message "Recommended for production deployments"
    
    # Check for strong passwords
    $mysqlPasswords = $envContent | Where-Object { $_ -match "^AMQ_DB_.*_PASSWORD=" }
    foreach ($password in $mysqlPasswords) {
        $value = ($password -split "=", 2)[1].Trim('"')
        $strongPassword = $value.Length -ge 16 -and $value -match "[A-Z]" -and $value -match "[a-z]" -and $value -match "[0-9]"
        $varName = ($password -split "=", 2)[0]
        Test-CheckResult -CheckName "$varName uses strong password" -Success $strongPassword -Message "Should be 16+ chars with mixed case and numbers"
    }
    
    Write-Host ""
}

function Test-ProjectStructure {
    Write-Host "Checking project structure..." -ForegroundColor Cyan
    
    # Essential directories
    $requiredDirs = @(
        "source",
        "source/Amiquin.Bot",
        "source/Amiquin.Core", 
        "source/Amiquin.Infrastructure",
        "Data",
        "Data/Logs",
        "Data/Database",
        "scripts"
    )
    
    foreach ($dir in $requiredDirs) {
        Test-FileExists -Path $dir -Description "Directory $dir"
    }
    
    # Solution file
    Test-FileExists -Path "source/source.sln" -Description "Solution file"
    
    # Docker files
    Test-FileExists -Path "docker-compose.yml" -Description "Docker Compose configuration"
    Test-FileExists -Path "source/Amiquin.Bot/dockerfile" -Description "Dockerfile"
    
    Write-Host ""
}

function Test-BuildStatus {
    Write-Host "Checking build status..." -ForegroundColor Cyan
    
    if (Test-Path "source/source.sln") {
        try {
            Set-Location "source"
            
            Write-Host "  Restoring packages..." -ForegroundColor Gray
            $restoreResult = dotnet restore source.sln --verbosity quiet 2>&1
            Test-CheckResult -CheckName "NuGet package restore" -Success ($LASTEXITCODE -eq 0)
            
            Write-Host "  Building solution..." -ForegroundColor Gray
            $buildResult = dotnet build source.sln --configuration Release --no-restore --verbosity quiet 2>&1
            Test-CheckResult -CheckName "Solution builds successfully" -Success ($LASTEXITCODE -eq 0)
            
            if ($Verbose -and $LASTEXITCODE -ne 0) {
                Write-Host "Build output:" -ForegroundColor Gray
                Write-Host $buildResult -ForegroundColor Gray
            }
            
        } finally {
            Set-Location $ProjectRoot
        }
    } else {
        Test-CheckResult -CheckName "Solution file exists" -Success $false -Message "Cannot test build without solution file"
    }
    
    Write-Host ""
}

# Run all validations
Test-ProjectStructure
Test-EnvironmentFile
Test-ExternalDependencies
Test-ServiceConnectivity
Test-SecurityConfiguration
Test-BuildStatus

# Summary
Write-Host "=== Validation Summary ===" -ForegroundColor Cyan
Write-Host "✓ Passed: $SuccessCount" -ForegroundColor Green
if ($WarningCount -gt 0) {
    Write-Host "⚠ Warnings: $WarningCount" -ForegroundColor Yellow
}
if ($ErrorCount -gt 0) {
    Write-Host "❌ Errors: $ErrorCount" -ForegroundColor Red
}

Write-Host ""

if ($ErrorCount -eq 0) {
    Write-Host "🎉 Setup validation completed successfully!" -ForegroundColor Green
    if ($WarningCount -gt 0) {
        Write-Host "Note: Some warnings were found but they do not prevent basic functionality." -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ Setup validation failed with $ErrorCount errors." -ForegroundColor Red
    Write-Host "Please address the errors above before running Amiquin." -ForegroundColor Yellow
    
    if (-not $Fix) {
        Write-Host "Tip: Run with -Fix to attempt automatic fixes where possible." -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "• Fix any errors or warnings shown above"
Write-Host "• Start required services: .\scripts\start-qdrant.ps1"
Write-Host "• Run the application: cd source/Amiquin.Bot; dotnet run"
Write-Host "• For production: .\scripts\deploy-production.ps1"

exit $ErrorCount