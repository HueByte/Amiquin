#!/bin/bash

# Script to start Qdrant vector database for Amiquin
# This script starts Qdrant in development mode with optional web UI

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo "Starting Qdrant vector database for Amiquin..."

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "Error: Docker is not running. Please start Docker first."
    exit 1
fi

# Navigate to project root
cd "$PROJECT_ROOT"

# Check command line arguments
INCLUDE_UI=false
DETACHED=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --ui)
            INCLUDE_UI=true
            shift
            ;;
        --detached|-d)
            DETACHED=true
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [--ui] [--detached|-d]"
            echo "  --ui         Include Qdrant web UI (development mode)"
            echo "  --detached   Run in detached mode"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Prepare Docker Compose command
COMPOSE_CMD="docker-compose"

if [ "$INCLUDE_UI" = true ]; then
    COMPOSE_CMD="$COMPOSE_CMD --profile qdrant-dev"
    echo "Starting Qdrant with Web UI..."
else
    COMPOSE_CMD="$COMPOSE_CMD --profile qdrant-only"
    echo "Starting Qdrant without Web UI..."
fi

if [ "$DETACHED" = true ]; then
    COMPOSE_CMD="$COMPOSE_CMD up -d"
else
    COMPOSE_CMD="$COMPOSE_CMD up"
fi

# Execute the command
echo "Running: $COMPOSE_CMD"
eval $COMPOSE_CMD

if [ "$DETACHED" = true ]; then
    echo ""
    echo "Qdrant is running in detached mode!"
    echo ""
    echo "Services:"
    echo "  - Qdrant gRPC API: localhost:6334"
    echo "  - Qdrant REST API: localhost:6333"
    
    if [ "$INCLUDE_UI" = true ]; then
        echo "  - Qdrant Web UI: http://localhost:3000"
    fi
    
    echo ""
    if [ "$INCLUDE_UI" = true ]; then
        echo "To view logs: docker-compose --profile qdrant-dev logs -f"
        echo "To stop: docker-compose --profile qdrant-dev down"
    else
        echo "To view logs: docker-compose --profile qdrant-only logs -f"
        echo "To stop: docker-compose --profile qdrant-only down"
    fi
fi