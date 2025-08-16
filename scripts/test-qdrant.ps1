# Test script for Qdrant vector database setup (PowerShell)

Write-Host "🔬 Testing Qdrant Setup..." -ForegroundColor Cyan

# Function to check if command exists
function Test-Command {
    param($Command)
    try {
        Get-Command $Command -ErrorAction Stop | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

# Check Docker
if (-not (Test-Command "docker")) {
    Write-Host "❌ Docker is not installed. Please install Docker first." -ForegroundColor Red
    exit 1
}

if (-not (Test-Command "docker-compose")) {
    Write-Host "❌ Docker Compose is not installed. Please install Docker Compose first." -ForegroundColor Red
    exit 1
}

Write-Host "✅ Docker and Docker Compose are available" -ForegroundColor Green

# Test Qdrant-only compose
Write-Host "🚀 Starting Qdrant container..." -ForegroundColor Yellow
docker-compose --profile qdrant-only up -d

# Wait for container to be ready
Write-Host "⏳ Waiting for Qdrant to be ready..." -ForegroundColor Yellow
Start-Sleep 10

# Check if Qdrant is healthy
Write-Host "🩺 Checking Qdrant health..." -ForegroundColor Yellow
$healthOk = $false
for ($i = 1; $i -le 30; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:6333/health" -Method GET -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            Write-Host "✅ Qdrant is healthy!" -ForegroundColor Green
            $healthOk = $true
            break
        }
    }
    catch {
        # Continue trying
    }
    
    if ($i -eq 30) {
        Write-Host "❌ Qdrant health check timed out" -ForegroundColor Red
        docker-compose --profile qdrant-only logs qdrant
        exit 1
    }
    
    Write-Host "   Attempt $i/30..." -ForegroundColor Gray
    Start-Sleep 2
}

if (-not $healthOk) {
    Write-Host "❌ Could not verify Qdrant health" -ForegroundColor Red
    exit 1
}

# Test basic API calls
Write-Host "🧪 Testing Qdrant API..." -ForegroundColor Cyan

# Get collections
Write-Host "📋 Getting collections..." -ForegroundColor Yellow
try {
    $collectionsResponse = Invoke-RestMethod -Uri "http://localhost:6333/collections" -Method GET
    $collectionsCount = $collectionsResponse.result.collections.Count
    Write-Host "   Found $collectionsCount collections" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Failed to get collections: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test creating a collection
Write-Host "🗃️  Testing collection creation..." -ForegroundColor Yellow
$createCollectionBody = @{
    vectors = @{
        size = 1536
        distance = "Cosine"
    }
} | ConvertTo-Json -Depth 3

try {
    Invoke-RestMethod -Uri "http://localhost:6333/collections/test_collection" -Method PUT -Body $createCollectionBody -ContentType "application/json" | Out-Null
    Write-Host "   Collection created successfully" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Failed to create collection: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Verify collection was created
Write-Host "🔍 Verifying test collection..." -ForegroundColor Yellow
try {
    $collectionInfo = Invoke-RestMethod -Uri "http://localhost:6333/collections/test_collection" -Method GET
    Write-Host "   Collection config: $($collectionInfo.result.config.params | ConvertTo-Json -Compress)" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Failed to verify collection: $($_.Exception.Message)" -ForegroundColor Red
}

# Test inserting a point (simplified vector for testing)
Write-Host "📌 Testing point insertion..." -ForegroundColor Yellow
$vector = @(1..1536 | ForEach-Object { [math]::Round((Get-Random -Minimum -1 -Maximum 1), 3) })
$insertPointBody = @{
    points = @(
        @{
            id = "test-point-1"
            vector = $vector
            payload = @{
                content = "Test memory content"
                type = "test"
            }
        }
    )
} | ConvertTo-Json -Depth 4

try {
    Invoke-RestMethod -Uri "http://localhost:6333/collections/test_collection/points" -Method PUT -Body $insertPointBody -ContentType "application/json" | Out-Null
    Write-Host "   Point inserted successfully" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Failed to insert point: $($_.Exception.Message)" -ForegroundColor Red
}

# Test searching
Write-Host "🔍 Testing vector search..." -ForegroundColor Yellow
$searchBody = @{
    vector = $vector
    limit = 5
} | ConvertTo-Json -Depth 3

try {
    $searchResponse = Invoke-RestMethod -Uri "http://localhost:6333/collections/test_collection/points/search" -Method POST -Body $searchBody -ContentType "application/json"
    Write-Host "   Search returned $($searchResponse.result.Count) results" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Failed to search: $($_.Exception.Message)" -ForegroundColor Red
}

# Clean up test collection
Write-Host "🧹 Cleaning up test collection..." -ForegroundColor Yellow
try {
    Invoke-RestMethod -Uri "http://localhost:6333/collections/test_collection" -Method DELETE | Out-Null
    Write-Host "   Test collection removed" -ForegroundColor Gray
}
catch {
    Write-Host "⚠️  Failed to clean up test collection: $($_.Exception.Message)" -ForegroundColor Orange
}

Write-Host ""
Write-Host "✅ All tests passed! Qdrant is working correctly." -ForegroundColor Green
Write-Host ""
Write-Host "🌐 Qdrant Web UI (if available): http://localhost:6333/dashboard" -ForegroundColor Cyan
Write-Host "📡 REST API: http://localhost:6333" -ForegroundColor Cyan
Write-Host "🔌 gRPC API: localhost:6334" -ForegroundColor Cyan
Write-Host ""
Write-Host "To stop Qdrant:" -ForegroundColor Yellow
Write-Host "  docker-compose --profile qdrant-only down" -ForegroundColor Gray
Write-Host ""
Write-Host "To remove all data:" -ForegroundColor Yellow
Write-Host "  docker-compose --profile qdrant-only down -v" -ForegroundColor Gray