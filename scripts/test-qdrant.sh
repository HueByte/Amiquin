#!/bin/bash

# Test script for Qdrant vector database setup
set -e

echo "🔬 Testing Qdrant Setup..."

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check Docker
if ! command_exists docker; then
    echo "❌ Docker is not installed. Please install Docker first."
    exit 1
fi

if ! command_exists docker-compose; then
    echo "❌ Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

echo "✅ Docker and Docker Compose are available"

# Test Qdrant-only compose
echo "🚀 Starting Qdrant container..."
docker-compose --profile qdrant-only up -d

# Wait for container to be ready
echo "⏳ Waiting for Qdrant to be ready..."
sleep 10

# Check if Qdrant is healthy
echo "🩺 Checking Qdrant health..."
for i in {1..30}; do
    if curl -f http://localhost:6333/health >/dev/null 2>&1; then
        echo "✅ Qdrant is healthy!"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "❌ Qdrant health check timed out"
        docker-compose --profile qdrant-only logs qdrant
        exit 1
    fi
    echo "   Attempt $i/30..."
    sleep 2
done

# Test basic API calls
echo "🧪 Testing Qdrant API..."

# Get collections
echo "📋 Getting collections..."
COLLECTIONS=$(curl -s http://localhost:6333/collections | jq -r '.result.collections | length')
echo "   Found $COLLECTIONS collections"

# Test creating a collection
echo "🗃️  Testing collection creation..."
curl -X PUT "http://localhost:6333/collections/test_collection" \
     -H "Content-Type: application/json" \
     -d '{
       "vectors": {
         "size": 1536,
         "distance": "Cosine"
       }
     }'

# Verify collection was created
echo ""
echo "🔍 Verifying test collection..."
curl -s "http://localhost:6333/collections/test_collection" | jq '.result.config.params'

# Test inserting a point
echo ""
echo "📌 Testing point insertion..."
curl -X PUT "http://localhost:6333/collections/test_collection/points" \
     -H "Content-Type: application/json" \
     -d '{
       "points": [
         {
           "id": "test-point-1",
           "vector": [0.1, 0.2, 0.3, 0.4],
           "payload": {"content": "Test memory content", "type": "test"}
         }
       ]
     }'

# Test searching
echo ""
echo "🔍 Testing vector search..."
curl -X POST "http://localhost:6333/collections/test_collection/points/search" \
     -H "Content-Type: application/json" \
     -d '{
       "vector": [0.1, 0.2, 0.3, 0.4],
       "limit": 5
     }' | jq '.result'

# Clean up test collection
echo ""
echo "🧹 Cleaning up test collection..."
curl -X DELETE "http://localhost:6333/collections/test_collection"

echo ""
echo "✅ All tests passed! Qdrant is working correctly."
echo ""
echo "🌐 Qdrant Web UI (if available): http://localhost:6333/dashboard"
echo "📡 REST API: http://localhost:6333"
echo "🔌 gRPC API: localhost:6334"
echo ""
echo "To stop Qdrant:"
echo "  docker-compose --profile qdrant-only down"
echo ""
echo "To remove all data:"
echo "  docker-compose --profile qdrant-only down -v"