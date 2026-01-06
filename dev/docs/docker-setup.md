# Docker Setup Guide for Amiquin

This guide explains how to set up and run Amiquin with its dependencies using Docker Compose.

## Quick Start

### 1. Prerequisites

- **Docker**: Install [Docker Desktop](https://www.docker.com/products/docker-desktop) or Docker Engine
- **Docker Compose**: Usually included with Docker Desktop

### 2. Configuration Setup

Copy the environment configuration:

```bash
# Copy environment variables
cp .env.example .env

# Edit .env with your configuration
# At minimum, set:
# - AMQ_Bot__Token (Discord bot token)
# - AMQ_LLM__Providers__OpenAI__ApiKey (OpenAI API key)
```

### 3. Running Services

#### Option A: Run All Services (Full Stack)

```bash
# Start everything (MySQL + Qdrant + Amiquin Bot)
docker-compose --profile full up -d

# View logs
docker-compose --profile full logs -f
```

#### Option B: Run Individual Components

```bash
# Start only database services
docker-compose --profile database up -d

# Start only Qdrant (vector database)
docker-compose --profile vector up -d
# or
docker-compose --profile memory up -d

# Start only the bot (requires external database)
docker-compose --profile bot up -d
```

#### Option C: Development Mode

```bash
# Start only Qdrant for local development
docker-compose --profile qdrant-only up -d

# Start Qdrant with Web UI
docker-compose --profile qdrant-dev up -d

# Run Amiquin locally with:
# dotnet run --project source/Amiquin.Bot
```

## Docker Compose Profiles

Amiquin uses Docker Compose profiles to enable different deployment scenarios with a single `docker-compose.yml` file:

### Development Profiles
- **`qdrant-only`**: Just Qdrant vector database for testing
- **`qdrant-dev`**: Qdrant with web UI for development
- **`database`**: Just MySQL database
- **`vector`/`memory`**: Alias for Qdrant-only
- **`bot`**: Just the Amiquin bot (requires external dependencies)
- **`dev`**: Full development stack (bot + mysql + qdrant + web UI)

### Production Profiles
- **`prod`**: Production stack (bot + mysql + qdrant with optimizations)
- **`prod-proxy`**: Production with nginx reverse proxy
- **`prod-cache`**: Production with redis caching
- **`prod-full`**: Production with all services (nginx + redis + monitoring)

### Usage Examples
```bash
# Development with web UI
docker-compose --profile dev up -d

# Production minimal
docker-compose --profile prod up -d

# Production with all features
docker-compose --profile prod-full up -d

# Just Qdrant for testing
docker-compose --profile qdrant-only up -d
```

## Service Details

### Qdrant Vector Database

- **Container Name**: `qdrant-{AMQ_BOT_NAME}`
- **Ports**:
  - `6333`: REST API
  - `6334`: gRPC API
  - `3000`: Web UI (if enabled)
- **Profiles**: `qdrant-only`, `qdrant-dev`, `vector`, `memory`, `dev`, `prod`, `prod-proxy`, `prod-cache`, `prod-full`
- **Data**: Persisted in `amq-qdrant-data` volume

**Access Points:**
- REST API: http://localhost:6333
- gRPC API: localhost:6334
- Web Dashboard: http://localhost:6333/dashboard

### MySQL Database

- **Container Name**: `mysql-{AMQ_BOT_NAME}`
- **Port**: `3306`
- **Profiles**: `database`, `dev`, `prod`, `prod-proxy`, `prod-cache`, `prod-full`
- **Data**: Persisted in `amq-db-data` volume

### Amiquin Bot

- **Container Name**: `bot-{AMQ_BOT_NAME}`
- **Port**: `10001` (mapped to internal `10000`)
- **Profiles**: `bot`, `dev`, `prod`, `prod-proxy`, `prod-cache`, `prod-full`
- **Dependencies**: MySQL and Qdrant must be healthy

## Environment Variables

### Core Configuration (Required)

```bash
# Bot Configuration
AMQ_Bot__Token=your-discord-bot-token-here
AMQ_LLM__Providers__OpenAI__ApiKey=sk-your-openai-api-key-here

# Docker Services
AMQ_BOT_NAME=amiquin-instance
```

### Database Configuration

```bash
# MySQL (when using Docker Compose)
AMQ_Database__Mode=0  # 0=MySQL, 1=SQLite
AMQ_DB_ROOT_PASSWORD=your-secure-root-password
AMQ_DB_NAME=amiquin_db
AMQ_DB_USER=amiquin_user
AMQ_DB_USER_PASSWORD=your-secure-user-password
```

### Memory System Configuration

```bash
# Memory System
AMQ_Memory__Enabled=true
AMQ_Memory__Qdrant__Host=qdrant  # Use service name in Docker
AMQ_Memory__Qdrant__Port=6334

# For local development (when running bot outside Docker):
# AMQ_Memory__Qdrant__Host=localhost
```

### Qdrant Docker Configuration

```bash
# Qdrant Service Ports
AMQ_QDRANT_HTTP_PORT=6333
AMQ_QDRANT_GRPC_PORT=6334
AMQ_QDRANT_WEB_UI_PORT=3000
AMQ_QDRANT_LOG_LEVEL=INFO
```

## Common Commands

### Starting Services

```bash
# Full stack
docker-compose --profile full up -d

# Just databases
docker-compose --profile database --profile vector up -d

# With logs
docker-compose --profile full up
```

### Viewing Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f qdrant
docker-compose logs -f mysql
docker-compose logs -f amiquinbot
```

### Stopping Services

```bash
# Stop all
docker-compose down

# Stop and remove volumes (⚠️ deletes all data)
docker-compose down -v
```

### Health Checks

```bash
# Check service status
docker-compose ps

# Test Qdrant health
curl http://localhost:6333/

# Test MySQL connection
docker-compose exec mysql mysql -u ${AMQ_DB_USER} -p${AMQ_DB_USER_PASSWORD} ${AMQ_DB_NAME}
```

## Testing Setup

Use the provided test scripts to verify your setup:

### Linux/macOS

```bash
# Test Qdrant only
./scripts/test-qdrant.sh

# Make executable if needed
chmod +x scripts/test-qdrant.sh
```

### Windows PowerShell

```powershell
# Test Qdrant only
.\scripts\test-qdrant.ps1
```

## Troubleshooting

### Common Issues

#### Container Won't Start

```bash
# Check logs
docker-compose logs [service-name]

# Check container status
docker-compose ps
```

#### Port Already in Use

```bash
# Find what's using the port
netstat -tulpn | grep :6333

# Kill the process or change ports in .env
```

#### Permission Issues

```bash
# Fix volume permissions (Linux)
sudo chown -R $USER:$USER ./Data

# On Windows, run Docker Desktop as Administrator
```

#### Memory Configuration Issues

1. **Wrong Host**: Use `qdrant` (service name) in Docker, `localhost` for local development
2. **Port Conflicts**: Check `AMQ_QDRANT_GRPC_PORT` and `AMQ_QDRANT_HTTP_PORT`
3. **API Key**: Only needed for cloud Qdrant instances

### Useful Commands

```bash
# Restart a service
docker-compose restart qdrant

# Rebuild containers
docker-compose build --no-cache

# Clean up everything
docker system prune -a

# View resource usage
docker stats
```

## Development Workflow

### For Local Development

1. Start only Qdrant and MySQL:
   ```bash
   docker-compose --profile database --profile vector up -d
   ```

2. Run Amiquin locally:
   ```bash
   dotnet run --project source/Amiquin.Bot
   ```

3. Use localhost for connection strings in your local config

### For Production

1. Use the full profile:
   ```bash
   docker-compose --profile full up -d
   ```

2. Monitor with:
   ```bash
   docker-compose logs -f
   ```

## Security Notes

- Change default passwords in `.env`
- Use secrets management for production
- Restrict network access to services
- Regularly update Docker images
- Monitor logs for suspicious activity

## Performance Tuning

### Qdrant Optimization

```bash
# In .env, adjust Qdrant settings
AMQ_QDRANT_LOG_LEVEL=WARN  # Reduce logging overhead
```

### MySQL Optimization

MySQL tuning is done via the configuration file at `mysql/conf.d/production.cnf`.
The file contains InnoDB, connection, and performance settings that are loaded
automatically when the MySQL container starts.

## Backup and Recovery

### Backup Data

```bash
# Backup volumes
docker run --rm -v amq-db-data:/data -v $(pwd):/backup alpine tar czf /backup/mysql-backup.tar.gz /data
docker run --rm -v amq-qdrant-data:/data -v $(pwd):/backup alpine tar czf /backup/qdrant-backup.tar.gz /data
```

### Restore Data

```bash
# Restore volumes
docker run --rm -v amq-db-data:/data -v $(pwd):/backup alpine tar xzf /backup/mysql-backup.tar.gz -C /
docker run --rm -v amq-qdrant-data:/data -v $(pwd):/backup alpine tar xzf /backup/qdrant-backup.tar.gz -C /
```