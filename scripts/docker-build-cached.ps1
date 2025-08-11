# =============================================================================
# Docker Build Script with Optimized Caching (PowerShell version)
# =============================================================================

param(
    [switch]$SkipDeps,
    [switch]$NoCache,
    [switch]$Verbose,
    [switch]$Help
)

# Configuration
$DOCKERFILE_PATH = "./source/Amiquin.Bot/dockerfile"
$BUILD_CONTEXT = "./source"

# Dependency versions (centralized for easy updates)  
$LIBSODIUM_VERSION = "1.0.20"
$OPUS_VERSION = "1.5.2"
$PIPER_VERSION = "1.2.0"

# Colors for output
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    } else {
        $input | Write-Output
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

function Write-Info($message) {
    Write-ColorOutput Green "[INFO] $message"
}

function Write-Warn($message) {
    Write-ColorOutput Yellow "[WARN] $message"
}

function Write-Err($message) {
    Write-ColorOutput Red "[ERROR] $message"
}

if ($Help) {
    Write-Output "Usage: .\docker-build-cached.ps1 [OPTIONS]"
    Write-Output "Options:"
    Write-Output "  -SkipDeps       Skip building dependency caches"
    Write-Output "  -NoCache        Build without using Docker cache"
    Write-Output "  -Verbose        Enable verbose build output"
    Write-Output "  -Help           Show this help message"
    exit 0
}

Write-ColorOutput Blue "=============================================================================="
Write-ColorOutput Blue "Amiquin Docker Build with Optimized Dependency Caching"
Write-ColorOutput Blue "=============================================================================="
Write-Output ""

# Function to check if image exists
function Test-ImageExists($imageName) {
    try {
        docker image inspect $imageName *>&1 | Out-Null
        return $true
    } catch {
        return $false
    }
}

# Function to build cached dependency
function Build-Dependency($stage, $imageName, $version, $description) {
    Write-Info "Building $description ($stage -> $imageName)..."
    
    if (Test-ImageExists $imageName) {
        Write-Warn "$description cache already exists. Use -NoCache to rebuild."
        return
    }
    
    $buildArgs = @(
        "build",
        "--target", $stage,
        "--tag", $imageName,
        "--build-arg", "$($stage.ToUpper() -replace '-BUILD','')_VERSION=$version",
        "-f", $DOCKERFILE_PATH,
        $BUILD_CONTEXT
    )
    
    & docker @buildArgs
    
    if ($LASTEXITCODE -eq 0) {
        Write-Info "✅ $description cache built successfully!"
        Write-Output ""
    } else {
        Write-Err "Failed to build $description cache"
        exit 1
    }
}

# Step 1: Build dependency caches (if not skipped)
if (-not $SkipDeps) {
    Write-Info "🔧 Building dependency caches..."
    Write-Output ""
    
    # Build libsodium cache
    Build-Dependency "libsodium-build" "amiquin-deps:libsodium-$LIBSODIUM_VERSION" $LIBSODIUM_VERSION "libsodium"
    
    # Build opus cache
    Build-Dependency "opus-build" "amiquin-deps:opus-$OPUS_VERSION" $OPUS_VERSION "opus"
    
    # Build runtime dependencies cache  
    Build-Dependency "runtime-deps" "amiquin-deps:piper-$PIPER_VERSION" $PIPER_VERSION "Piper TTS"
    
    Write-Info "✅ All dependency caches built successfully!"
    Write-Output ""
} else {
    Write-Warn "Skipping dependency cache builds..."
}

# Step 2: Build main application
Write-Info "🚀 Building main Amiquin application..."
Write-Output ""

$buildArgs = @(
    "build",
    "--tag", "amiquin:latest",
    "--tag", "amiquin:$(Get-Date -Format 'yyyyMMdd_HHmmss')",
    "--build-arg", "LIBSODIUM_VERSION=$LIBSODIUM_VERSION",
    "--build-arg", "OPUS_VERSION=$OPUS_VERSION", 
    "--build-arg", "PIPER_VERSION=$PIPER_VERSION",
    "-f", $DOCKERFILE_PATH,
    $BUILD_CONTEXT
)

if ($NoCache) {
    $buildArgs += "--no-cache"
}

if ($Verbose) {
    $buildArgs += "--progress=plain"
}

& docker @buildArgs

if ($LASTEXITCODE -eq 0) {
    Write-Info "✅ Amiquin application built successfully!"
    Write-Output ""
} else {
    Write-Err "Failed to build main application"
    exit 1
}

# Step 3: Show build summary
Write-Info "📊 Build Summary:"
Write-Output "   • libsodium version: $LIBSODIUM_VERSION"
Write-Output "   • opus version: $OPUS_VERSION"
Write-Output "   • Piper TTS version: $PIPER_VERSION"
Write-Output "   • Final image: amiquin:latest"
Write-Output ""

# Step 4: Show cache status
Write-Info "💾 Dependency Cache Status:"
$images = @(
    "amiquin-deps:libsodium-$LIBSODIUM_VERSION",
    "amiquin-deps:opus-$OPUS_VERSION", 
    "amiquin-deps:piper-$PIPER_VERSION"
)

foreach ($image in $images) {
    if (Test-ImageExists $image) {
        Write-Output "   ✅ $image"
    } else {
        Write-Output "   ❌ $image"
    }
}
Write-Output ""

# Step 5: Show next steps  
Write-Info "🎯 Next Steps:"
Write-Output "   1. Run with: docker run -d --name amiquin amiquin:latest"
Write-Output "   2. Or use:   docker-compose -f docker-compose.cached.yml up -d"
Write-Output "   3. Logs:     docker logs amiquin"
Write-Output ""
Write-ColorOutput Green "Build completed successfully! 🎉"