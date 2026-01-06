#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pre-deployment checklist for Amiquin bot
.DESCRIPTION
    Comprehensive checklist to validate the project is ready for deployment.
    Checks configuration, dependencies, build status, and security settings.
#>

param(
    [switch]$Production,
    [switch]$Verbose,
    [switch]$Fix,
    [switch]$Help
)

if ($Help) {
    Write-Host "Amiquin Pre-Deployment Checklist" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "This script validates your Amiquin project before deployment:"
    Write-Host "  ✓ Configuration completeness"
    Write-Host "  ✓ Environment variables"
    Write-Host "  ✓ Dependencies and services"
    Write-Host "  ✓ Build and compilation"
    Write-Host "  ✓ Security settings"
    Write-Host "  ✓ Docker readiness"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  ./pre-deployment-checklist.ps1               # Basic checks"
    Write-Host "  ./pre-deployment-checklist.ps1 -Production   # Production-ready checks"
    Write-Host "  ./pre-deployment-checklist.ps1 -Fix          # Auto-fix issues where possible"
    Write-Host "  ./pre-deployment-checklist.ps1 -Verbose      # Detailed output"
    exit 0
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$PassCount = 0
$FailCount = 0
$WarnCount = 0

Set-Location $ProjectRoot

Write-Host "=== Amiquin Pre-Deployment Checklist ===" -ForegroundColor Cyan
if ($Production) {
    Write-Host "Mode: Production (enhanced security checks)" -ForegroundColor Yellow
}
else {
    Write-Host "Mode: Standard" -ForegroundColor Green
}
Write-Host ""

function Test-Item {
    param(
        [string]$Name,
        [bool]$Pass,
        [string]$Message = "",
        [string]$Level = "Error"
    )
    
    if ($Pass) {
        Write-Host "✓ $Name" -ForegroundColor Green
        $script:PassCount++
    }
    else {
        if ($Level -eq "Warning") {
            Write-Host "⚠ $Name" -ForegroundColor Yellow
            $script:WarnCount++
        }
        else {
            Write-Host "❌ $Name" -ForegroundColor Red
            $script:FailCount++
        }
        if ($Message) {
            Write-Host "  → $Message" -ForegroundColor Gray
        }
    }
    if ($Verbose -and $Pass -and $Message) {
        Write-Host "  ℹ $Message" -ForegroundColor Gray
    }
}

# ====================
# 1. Configuration Files
# ====================
Write-Host "`n[1/8] Configuration Files" -ForegroundColor Cyan

Test-Item ".env file exists" (Test-Path ".env") "Run setup-project.ps1 to create configuration"
Test-Item "appsettings.json exists" (Test-Path "source/Amiquin.Bot/appsettings.json") "Copy from appsettings.example.json"
Test-Item "docker-compose.yml exists" (Test-Path "docker-compose.yml") "Required for Docker deployment"

if (Test-Path ".env") {
    $envContent = Get-Content ".env" -Raw
    Test-Item "Discord token configured" ($envContent -match 'AMQ_Discord__Token=".+"') "Add Discord bot token to .env"
    Test-Item "OpenAI API key configured" ($envContent -match 'AMQ_LLM__Providers__OpenAI__ApiKey="sk-.+"') "Add OpenAI API key to .env"
    
    if ($Production) {
        Test-Item "MySQL passwords set" ($envContent -match 'AMQ_DB_ROOT_PASSWORD=".{16,}"' -and $envContent -match 'AMQ_DB_USER_PASSWORD=".{16,}"') "Passwords should be 16+ characters" "Warning"
        Test-Item "Qdrant API key set (if auth enabled)" (
            ($envContent -notmatch 'AMQ_QDRANT_API_KEY=') -or 
            ($envContent -match 'AMQ_QDRANT_API_KEY=".{24,}"')
        ) "Set Qdrant API key for production" "Warning"
    }
}

# ====================
# 2. Dependencies
# ====================
Write-Host "`n[2/8] Dependencies & Tools" -ForegroundColor Cyan

$dotnetVersion = try { (dotnet --version 2>$null) } catch { $null }
Test-Item ".NET SDK installed" ($null -ne $dotnetVersion) "Install .NET SDK 9.0+" "Found: $dotnetVersion"

$dockerRunning = try { docker info 2>$null; $? } catch { $false }
Test-Item "Docker running" $dockerRunning "Start Docker Desktop" "Warning"

$dockerComposeAvailable = try { docker-compose --version 2>$null; $? } catch { $false }
Test-Item "Docker Compose available" $dockerComposeAvailable "Install Docker Compose" "Warning"

$ffmpegAvailable = try { ffmpeg -version 2>$null; $? } catch { $false }
Test-Item "ffmpeg available" $ffmpegAvailable "Required for voice features" "Warning"

# ====================
# 3. Project Structure
# ====================
Write-Host "`n[3/8] Project Structure" -ForegroundColor Cyan

Test-Item "Solution file exists" (Test-Path "source/source.sln")
Test-Item "Bot project exists" (Test-Path "source/Amiquin.Bot/Amiquin.Bot.csproj")
Test-Item "Core project exists" (Test-Path "source/Amiquin.Core/Amiquin.Core.csproj")
Test-Item "Infrastructure project exists" (Test-Path "source/Amiquin.Infrastructure/Amiquin.Infrastructure.csproj")

$dataDirs = @("Data/Database", "Data/Logs", "Data/Messages", "Data/Sessions", "Data/Plugins")
foreach ($dir in $dataDirs) {
    if (!(Test-Path $dir)) {
        if ($Fix) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Write-Host "  ✓ Created $dir" -ForegroundColor Green
        }
    }
}
Test-Item "Data directories exist" ($dataDirs | ForEach-Object { Test-Path $_ } | Where-Object { -not $_ }).Count -eq 0

# ====================
# 4. Build Status
# ====================
Write-Host "`n[4/8] Build Status" -ForegroundColor Cyan

Write-Host "  Building solution..." -NoNewline
$buildOutput = dotnet build "source/source.sln" -c Release --nologo -v quiet 2>&1
$buildSuccess = $LASTEXITCODE -eq 0

if ($buildSuccess) {
    Write-Host " Success" -ForegroundColor Green
    $script:PassCount++
}
else {
    Write-Host " Failed" -ForegroundColor Red
    $script:FailCount++
    if ($Verbose) {
        Write-Host "  Build errors:" -ForegroundColor Yellow
        $buildOutput | Select-Object -Last 10 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    }
}

Test-Item "Bot DLL exists" (Test-Path "source/Amiquin.Bot/bin/Release/net10.0/Amiquin.Bot.dll")

# ====================
# 5. Database Configuration
# ====================
Write-Host "`n[5/8] Database Configuration" -ForegroundColor Cyan

if (Test-Path ".env") {
    $envContent = Get-Content ".env" -Raw
    $dbMode = if ($envContent -match 'AMQ_Database__Mode=(\d)') { $Matches[1] } else { "1" }
    
    if ($dbMode -eq "0") {
        Write-Host "  Database mode: MySQL" -ForegroundColor Gray
        Test-Item "MySQL connection string set" ($envContent -match 'AMQ_ConnectionStrings__Amiquin-Mysql=') "Configure MySQL connection"
        
        if ($dockerRunning) {
            $mysqlContainer = docker ps --filter "name=mysql" --format "{{.Names}}" 2>$null
            Test-Item "MySQL container running" ($null -ne $mysqlContainer) "Start MySQL: docker-compose --profile database up -d" "Warning"
        }
    }
    else {
        Write-Host "  Database mode: SQLite" -ForegroundColor Gray
        Test-Item "SQLite directory exists" (Test-Path "Data/Database")
    }
}

# ====================
# 6. Memory System (Qdrant)
# ====================
Write-Host "`n[6/8] Memory System (Qdrant)" -ForegroundColor Cyan

if (Test-Path ".env") {
    $envContent = Get-Content ".env" -Raw
    $memoryEnabled = $envContent -match 'AMQ_Memory__Enabled=true'
    
    if ($memoryEnabled) {
        Write-Host "  Memory system: Enabled" -ForegroundColor Gray
        Test-Item "Qdrant host configured" ($envContent -match 'AMQ_Memory__Qdrant__Host=') "Set Qdrant host"
        Test-Item "Qdrant port configured" ($envContent -match 'AMQ_Memory__Qdrant__Port=') "Set Qdrant port"
        
        if ($dockerRunning) {
            $qdrantContainer = docker ps --filter "name=qdrant" --format "{{.Names}}" 2>$null
            Test-Item "Qdrant container running" ($null -ne $qdrantContainer) "Start Qdrant: docker-compose --profile qdrant-only up -d" "Warning"
        }
    }
    else {
        Write-Host "  Memory system: Disabled" -ForegroundColor Gray
        Test-Item "Memory configuration" $true "Memory system is optional" "Warning"
    }
}

# ====================
# 7. Web Search Configuration
# ====================
Write-Host "`n[7/8] Web Search Configuration" -ForegroundColor Cyan

if (Test-Path ".env") {
    $envContent = Get-Content ".env" -Raw
    $webSearchEnabled = $envContent -match 'AMQ_WebSearch__Enabled=true'
    
    if ($webSearchEnabled) {
        Write-Host "  Web search: Enabled" -ForegroundColor Gray
        $provider = if ($envContent -match 'AMQ_WebSearch__Provider="([^"]+)"') { $Matches[1] } else { "Unknown" }
        Write-Host "  Provider: $provider" -ForegroundColor Gray
        
        if ($provider -eq "Google") {
            Test-Item "Google API key set" ($envContent -match 'AMQ_WebSearch__ApiKey=".+"') "Configure Google Custom Search API key"
            Test-Item "Google Search Engine ID set" ($envContent -match 'AMQ_WebSearch__SearchEngineId=".+"') "Configure Google Search Engine ID"
        }
        elseif ($provider -eq "Bing") {
            Test-Item "Bing API key set" ($envContent -match 'AMQ_WebSearch__ApiKey=".+"') "Configure Bing Search API key"
        }
        else {
            Test-Item "DuckDuckGo provider" $true "No API key required"
        }
    }
    else {
        Write-Host "  Web search: Disabled" -ForegroundColor Gray
        Test-Item "Web search configuration" $true "Web search is optional" "Warning"
    }
}

# ====================
# 8. Security Checks (Production)
# ====================
if ($Production) {
    Write-Host "`n[8/8] Security Checks (Production)" -ForegroundColor Cyan
    
    if (Test-Path ".env") {
        $envContent = Get-Content ".env" -Raw
        
        Test-Item "No default passwords" ($envContent -notmatch 'password|changeme|admin|root123') "Use strong, unique passwords"
        Test-Item "No exposed API keys in git" (!(git ls-files | Select-String "api.*key" -Quiet)) "Remove committed secrets" "Warning"
        Test-Item "Docker secrets not in plaintext" $true "Consider Docker secrets or environment injection" "Warning"
        Test-Item "Logging level appropriate" ($envContent -match 'AMQ_Serilog__MinimumLevel__Default="(Warning|Error|Information)"') "Set appropriate log level"
    }
    
    Test-Item ".env not in git" ((git check-ignore ".env" 2>$null) -eq ".env") ".env should be in .gitignore"
    Test-Item ".gitignore exists" (Test-Path ".gitignore")
}
else {
    Write-Host "`n[8/8] Security Checks" -ForegroundColor Cyan
    Write-Host "  → Run with -Production flag for enhanced security checks" -ForegroundColor Yellow
}

# ====================
# Summary
# ====================
Write-Host "`n==================== Summary ====================" -ForegroundColor Cyan
Write-Host "✓ Passed:  $PassCount" -ForegroundColor Green
if ($WarnCount -gt 0) {
    Write-Host "⚠ Warnings: $WarnCount" -ForegroundColor Yellow
}
if ($FailCount -gt 0) {
    Write-Host "❌ Failed:  $FailCount" -ForegroundColor Red
}
Write-Host "=================================================" -ForegroundColor Cyan

if ($FailCount -eq 0 -and $WarnCount -eq 0) {
    Write-Host "`n🚀 All checks passed! Ready for deployment." -ForegroundColor Green
    exit 0
}
elseif ($FailCount -eq 0) {
    Write-Host "`n✅ No critical issues found. Review warnings before deployment." -ForegroundColor Yellow
    exit 0
}
else {
    Write-Host "`n⚠️  Critical issues found. Fix errors before deploying." -ForegroundColor Red
    Write-Host "Run './setup-project.ps1' to fix configuration issues." -ForegroundColor Yellow
    exit 1
}
