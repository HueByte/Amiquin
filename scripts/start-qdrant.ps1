# PowerShell script to start Qdrant vector database for Amiquin
# This script starts Qdrant in development mode with optional web UI

param(
    [switch]$UI = $false,
    [switch]$Detached = $false,
    [switch]$Help = $false
)

if ($Help) {
    Write-Host "Usage: .\start-qdrant.ps1 [-UI] [-Detached]"
    Write-Host "  -UI         Include Qdrant web UI (development mode)"
    Write-Host "  -Detached   Run in detached mode"
    exit 0
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

Write-Host "Starting Qdrant vector database for Amiquin..."

# Check if Docker is running
try {
    $null = docker info 2>$null
} catch {
    Write-Error "Docker is not running. Please start Docker first."
    exit 1
}

# Navigate to project root
Set-Location $ProjectRoot

# Prepare Docker Compose command
$ComposeArgs = @()

if ($UI) {
    $ComposeArgs += "--profile", "qdrant-dev"
    Write-Host "Starting Qdrant with Web UI..."
} else {
    $ComposeArgs += "--profile", "qdrant-only"
    Write-Host "Starting Qdrant without Web UI..."
}

if ($Detached) {
    $ComposeArgs += "up", "-d"
} else {
    $ComposeArgs += "up"
}

# Execute the command
Write-Host "Running: docker-compose $($ComposeArgs -join ' ')"
& docker-compose @ComposeArgs

if ($Detached) {
    Write-Host ""
    Write-Host "Qdrant is running in detached mode!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Services:"
    Write-Host "  - Qdrant gRPC API: localhost:6334"
    Write-Host "  - Qdrant REST API: localhost:6333"
    
    if ($UI) {
        Write-Host "  - Qdrant Web UI: http://localhost:3000"
    }
    
    Write-Host ""
    if ($UI) {
        Write-Host "To view logs: docker-compose --profile qdrant-dev logs -f"
        Write-Host "To stop: docker-compose --profile qdrant-dev down"
    } else {
        Write-Host "To view logs: docker-compose --profile qdrant-only logs -f"
        Write-Host "To stop: docker-compose --profile qdrant-only down"
    }
}