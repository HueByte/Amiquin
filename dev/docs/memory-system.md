# Memory System Guide

This guide explains Amiquin's memory system using Qdrant vector database for intelligent conversation context.

## Overview

Amiquin now uses Qdrant as a dedicated vector database for conversation memories, providing:
- **Better Performance**: Optimized vector similarity search
- **Scalability**: Handles large numbers of memories efficiently  
- **Persistence**: Data survives application restarts
- **Learning Opportunity**: Experience with production vector databases

## Prerequisites

1. **Docker**: Required to run Qdrant
2. **OpenAI API Key**: For generating embeddings

## Setup Steps

### 1. Start Qdrant

#### Option A: Using provided scripts
```bash
# Linux/macOS
./scripts/start-qdrant.sh --ui --detached

# Windows PowerShell
.\scripts\start-qdrant.ps1 -UI -Detached
```

#### Option B: Using Docker Compose
```bash
# Start Qdrant only
docker-compose --profile vector up -d

# Or start full stack (includes MySQL and bot)
docker-compose --profile full up -d
```

### 2. Configure Amiquin

Configure memory system using environment variables in your `.env` file:

```env
# Enable Memory System
AMQ_Memory__Enabled=true
AMQ_Memory__MaxMemoriesPerSession=1000
AMQ_Memory__MaxContextMemories=10
AMQ_Memory__SimilarityThreshold=0.7
AMQ_Memory__MinImportanceScore=0.3
AMQ_Memory__EmbeddingModel=text-embedding-3-small

# Qdrant Configuration
AMQ_Memory__Qdrant__Host=localhost
AMQ_Memory__Qdrant__Port=6334
AMQ_Memory__Qdrant__CollectionName=amiquin_memories
AMQ_Memory__Qdrant__VectorSize=1536
AMQ_Memory__Qdrant__AutoCreateCollection=true
```

### 3. Verify Setup

1. **Check Qdrant Health**: Visit `http://localhost:6333/dashboard` (if using UI)
2. **Start Amiquin**: The collection will be created automatically on first run
3. **Test Memory Creation**: Have conversations with the bot to generate memories

## Data Migration

Since this is a new implementation, existing conversation data in the SQL database will remain, but no automatic migration is provided. The bot will start building new memories from fresh conversations.

If you need to migrate existing conversation data:

1. Export conversation history from your database
2. Use the memory extraction APIs to process historical conversations
3. The system will automatically generate embeddings and store them in Qdrant

## Monitoring

- **Qdrant Web Dashboard**: `http://localhost:6333/dashboard`
- **Qdrant REST API**: `http://localhost:6333`
- **Qdrant gRPC API**: `localhost:6334`

Check collection status:
```bash
curl http://localhost:6333/collections/amiquin_memories
```

## Troubleshooting

### Qdrant Connection Issues
1. Verify Docker is running: `docker ps`
2. Check Qdrant logs: `docker-compose logs qdrant`
3. Verify network connectivity: `curl http://localhost:6333/health`
4. Check if Qdrant port is accessible: `curl http://localhost:6334` (gRPC port)

### Memory Creation Failures
1. Check OpenAI API key configuration
2. Verify embedding model availability
3. Review application logs for detailed error messages

### Performance Issues
1. Monitor Qdrant resource usage
2. Adjust `VectorSize` to match your embedding model
3. Consider tuning `SimilarityThreshold` based on your use case

## Architecture Benefits

The new Qdrant-based implementation provides:

- **Vector Similarity Search**: Fast, accurate semantic matching
- **Metadata Filtering**: Search by session, type, importance, etc.
- **Scalable Storage**: Handle millions of memories
- **Real-time Analytics**: Query collection statistics and health
- **Production Ready**: Built for high-availability environments

## Development

For development with the web UI:
```bash
./scripts/start-qdrant.sh --ui
```

This provides a visual interface to explore your vector data and understand how memories are stored and retrieved.