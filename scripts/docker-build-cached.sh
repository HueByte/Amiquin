#!/bin/bash

# =============================================================================
# Docker Build Script with Optimized Caching
# =============================================================================

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
DOCKERFILE_PATH="./source/Amiquin.Bot/dockerfile"
BUILD_CONTEXT="./source"

# Dependency versions (centralized for easy updates)
LIBSODIUM_VERSION="1.0.20"
OPUS_VERSION="1.5.2"
PIPER_VERSION="1.2.0"

echo -e "${BLUE}==============================================================================${NC}"
echo -e "${BLUE}Amiquin Docker Build with Optimized Dependency Caching${NC}"
echo -e "${BLUE}==============================================================================${NC}"
echo

# Function to print status
print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check if image exists
image_exists() {
    docker image inspect "$1" &> /dev/null
}

# Function to build cached dependency
build_dependency() {
    local stage=$1
    local image_name=$2
    local version=$3
    local description=$4
    
    print_status "Building $description ($stage -> $image_name)..."
    
    if image_exists "$image_name"; then
        print_warning "$description cache already exists. Use --no-cache to rebuild."
        return 0
    fi
    
    docker build \
        --target "$stage" \
        --tag "$image_name" \
        --build-arg "${stage^^}_VERSION=$version" \
        -f "$DOCKERFILE_PATH" \
        "$BUILD_CONTEXT"
        
    print_status "✅ $description cache built successfully!"
    echo
}

# Parse command line arguments
SKIP_DEPS=false
NO_CACHE=""
VERBOSE=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-deps)
            SKIP_DEPS=true
            shift
            ;;
        --no-cache)
            NO_CACHE="--no-cache"
            shift
            ;;
        --verbose)
            VERBOSE="--progress=plain"
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  --skip-deps     Skip building dependency caches"
            echo "  --no-cache      Build without using Docker cache"
            echo "  --verbose       Enable verbose build output"
            echo "  -h, --help      Show this help message"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Step 1: Build dependency caches (if not skipped)
if [ "$SKIP_DEPS" = false ]; then
    print_status "🔧 Building dependency caches..."
    echo
    
    # Build libsodium cache
    build_dependency "libsodium-build" "amiquin-deps:libsodium-$LIBSODIUM_VERSION" "$LIBSODIUM_VERSION" "libsodium"
    
    # Build opus cache  
    build_dependency "opus-build" "amiquin-deps:opus-$OPUS_VERSION" "$OPUS_VERSION" "opus"
    
    # Build runtime dependencies cache
    build_dependency "runtime-deps" "amiquin-deps:piper-$PIPER_VERSION" "$PIPER_VERSION" "Piper TTS"
    
    print_status "✅ All dependency caches built successfully!"
    echo
else
    print_warning "Skipping dependency cache builds..."
fi

# Step 2: Build main application
print_status "🚀 Building main Amiquin application..."
echo

docker build \
    $NO_CACHE \
    $VERBOSE \
    --tag amiquin:latest \
    --tag amiquin:$(date +%Y%m%d_%H%M%S) \
    --build-arg LIBSODIUM_VERSION="$LIBSODIUM_VERSION" \
    --build-arg OPUS_VERSION="$OPUS_VERSION" \
    --build-arg PIPER_VERSION="$PIPER_VERSION" \
    -f "$DOCKERFILE_PATH" \
    "$BUILD_CONTEXT"

print_status "✅ Amiquin application built successfully!"
echo

# Step 3: Show build summary
print_status "📊 Build Summary:"
echo -e "   ${GREEN}•${NC} libsodium version: $LIBSODIUM_VERSION"
echo -e "   ${GREEN}•${NC} opus version: $OPUS_VERSION"  
echo -e "   ${GREEN}•${NC} Piper TTS version: $PIPER_VERSION"
echo -e "   ${GREEN}•${NC} Final image: amiquin:latest"
echo

# Step 4: Show cache status
print_status "💾 Dependency Cache Status:"
for image in "amiquin-deps:libsodium-$LIBSODIUM_VERSION" "amiquin-deps:opus-$OPUS_VERSION" "amiquin-deps:piper-$PIPER_VERSION"; do
    if image_exists "$image"; then
        echo -e "   ${GREEN}✅${NC} $image"
    else
        echo -e "   ${RED}❌${NC} $image"
    fi
done
echo

# Step 5: Show next steps
print_status "🎯 Next Steps:"
echo "   1. Run with: docker run -d --name amiquin amiquin:latest"
echo "   2. Or use:   docker-compose -f docker-compose.cached.yml up -d"
echo "   3. Logs:     docker logs amiquin"
echo
echo -e "${GREEN}Build completed successfully! 🎉${NC}"