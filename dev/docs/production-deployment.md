# Production Deployment Guide for Amiquin

This guide covers deploying Amiquin in a production environment with high availability, security, and performance optimizations.

## Quick Start

### 1. Production Configuration

```bash
# Copy production environment template
cp .env.production .env

# Edit .env with your production values
# CRITICAL: Update these values before deployment:
# - AMQ_Bot__Token (Discord bot token)
# - AMQ_LLM__Providers__OpenAI__ApiKey (OpenAI API key)
# - AMQ_DB_ROOT_PASSWORD (Strong MySQL root password)
# - AMQ_DB_USER_PASSWORD (Strong MySQL user password)
# - AMQ_QDRANT_API_KEY (Optional Qdrant API key for security)
```

### 2. Deploy Full Stack

```bash
# Deploy with all production services
docker-compose -f docker-compose.production.yml --profile full up -d

# Or with proxy and caching
docker-compose -f docker-compose.production.yml --profile full-proxy --profile full-cache up -d
```

### 3. Verify Deployment

```bash
# Check service health
docker-compose -f docker-compose.production.yml ps

# Check logs
docker-compose -f docker-compose.production.yml logs -f

# Test health endpoints
curl http://localhost/health
curl http://localhost:6333/health  # Qdrant health
```

## Production Architecture

### Service Overview

| Service | Purpose | Port | Resources |
|---------|---------|------|-----------|
| **amiquinbot** | Main Discord bot application | 10001 | 4GB RAM, 2 CPU |
| **qdrant** | Vector database for memories | 6333/6334 | 8GB RAM, 4 CPU |
| **mysql** | Primary database | 3306 | 4GB RAM, 2 CPU |
| **nginx** | Reverse proxy & load balancer | 80/443 | 512MB RAM |
| **redis** | Caching & session storage | 6379 | 2GB RAM |

### Network Architecture

```
Internet → Nginx (80/443) → Amiquin Bot (10000)
                         ↓
                      Qdrant (6333/6334)
                         ↓
                      MySQL (3306)
                         ↓
                      Redis (6379)
```

## Configuration Details

### Environment Variables

#### Critical Production Settings

```bash
# Discord & AI
AMQ_Bot__Token="your-production-discord-bot-token"
AMQ_LLM__Providers__OpenAI__ApiKey="sk-your-production-openai-key"

# Database Security
AMQ_DB_ROOT_PASSWORD="your-very-secure-root-password-32-chars-min"
AMQ_DB_USER_PASSWORD="your-very-secure-user-password-32-chars-min"

# Qdrant Security
AMQ_QDRANT_API_KEY="your-qdrant-api-key-for-security"

# Instance Naming
AMQ_BOT_NAME="amiquin-prod"
```

#### Memory System Optimization

```bash
# Production-optimized memory settings
AMQ_Memory__Enabled=true
AMQ_Memory__MaxMemoriesPerSession=2000      # Increased for production
AMQ_Memory__MaxContextMemories=15           # More context for better responses
AMQ_Memory__SimilarityThreshold=0.75        # Higher threshold for quality
AMQ_Memory__MinImportanceScore=0.4          # Higher importance filter
AMQ_Memory__CleanupOlderThanDays=90         # Longer retention in production
```

#### Qdrant Performance Tuning

```bash
# Performance settings
QDRANT_MAX_REQUEST_SIZE_MB=64
QDRANT_MAX_WORKERS=0                        # Auto-detect CPU cores
QDRANT_WAL_CAPACITY_MB=64                   # Write-ahead log capacity
QDRANT_MEMMAP_THRESHOLD_KB=200000           # Memory mapping threshold
QDRANT_INDEXING_THRESHOLD_KB=20000          # Index creation threshold

# HNSW Index optimization
QDRANT_HNSW_M=16                           # Index connectivity
QDRANT_HNSW_EF_CONSTRUCT=200               # Build-time accuracy
QDRANT_HNSW_FULL_SCAN_THRESHOLD=20000      # Full scan threshold
```

#### Resource Limits

```bash
# Container resource limits
QDRANT_MEMORY_LIMIT=8G
QDRANT_MEMORY_RESERVATION=4G
AMIQUIN_MEMORY_LIMIT=4G
AMIQUIN_MEMORY_RESERVATION=1G
# MySQL tuning is done via mysql/conf.d/production.cnf
```

### MySQL Production Configuration

The production setup includes optimized MySQL settings in `mysql/conf.d/production.cnf`:

- **Buffer Pool**: 2GB for caching data and indexes
- **Connection Pooling**: Up to 300 concurrent connections
- **Binary Logging**: Enabled for replication and recovery
- **Slow Query Log**: Track queries taking >2 seconds
- **InnoDB Optimization**: Enhanced for concurrent workloads

### Qdrant Production Features

- **WAL (Write-Ahead Logging)**: Ensures data durability
- **Snapshots**: Automatic backup capability
- **HNSW Indexing**: Optimized for fast similarity search
- **Resource Management**: Memory limits and CPU optimization
- **Security**: Optional API key protection

## Security Hardening

### 1. Network Security

```bash
# Configure firewall (example for Ubuntu)
sudo ufw allow 22      # SSH
sudo ufw allow 80      # HTTP
sudo ufw allow 443     # HTTPS
sudo ufw deny 3306     # Block direct MySQL access
sudo ufw deny 6333     # Block direct Qdrant access
sudo ufw deny 6334     # Block direct Qdrant gRPC
sudo ufw enable
```

### 2. SSL/TLS Configuration

```bash
# Generate SSL certificates (example with Let's Encrypt)
sudo apt install certbot
sudo certbot certonly --standalone -d your-domain.com

# Copy certificates to nginx/ssl/
sudo cp /etc/letsencrypt/live/your-domain.com/fullchain.pem nginx/ssl/cert.pem
sudo cp /etc/letsencrypt/live/your-domain.com/privkey.pem nginx/ssl/key.pem

# Enable HTTPS in nginx configuration
# Uncomment the HTTPS server block in nginx/nginx.conf
```

### 3. API Security

```bash
# Set strong API keys
AMQ_QDRANT_API_KEY="$(openssl rand -base64 32)"

# Update nginx configuration
# Edit nginx/nginx.conf and set the X-API-Key header check
```

### 4. Database Security

```bash
# Use strong passwords (32+ characters)
AMQ_DB_ROOT_PASSWORD="$(openssl rand -base64 32)"
AMQ_DB_USER_PASSWORD="$(openssl rand -base64 32)"

# Enable SSL for MySQL connections
# Add to connection strings: SslMode=Required
```

## Monitoring & Logging

### Health Checks

All services include health checks:

```bash
# Application health
curl http://localhost/health

# Qdrant health
curl http://localhost:6333/health

# MySQL health (from container)
docker-compose -f docker-compose.production.yml exec mysql mysqladmin ping
```

### Log Management

```bash
# View service logs
docker-compose -f docker-compose.production.yml logs -f amiquinbot
docker-compose -f docker-compose.production.yml logs -f qdrant
docker-compose -f docker-compose.production.yml logs -f mysql

# Log rotation is automatically configured with:
# - 10MB max file size
# - 3-5 files retained per service
```

### Metrics Collection

Production configuration includes:

- **Application metrics** on port 9090
- **Docker container stats** via `docker stats`
- **Nginx access logs** for request monitoring
- **MySQL slow query logs** for performance analysis

## Backup & Recovery

### 1. Automated Backups

```bash
# Database backup script (daily at 2 AM)
# Set in .env:
BACKUP_ENABLED=true
BACKUP_SCHEDULE="0 2 * * *"
BACKUP_RETENTION_DAYS=30
```

### 2. Manual Backups

```bash
# MySQL backup
docker-compose -f docker-compose.production.yml exec mysql mysqldump \
  -u root -p${AMQ_DB_ROOT_PASSWORD} ${AMQ_DB_NAME} > backup_$(date +%Y%m%d).sql

# Qdrant backup
docker run --rm -v amiquin_amq-qdrant-data:/data -v $(pwd):/backup alpine \
  tar czf /backup/qdrant_backup_$(date +%Y%m%d).tar.gz /data

# Application data backup
docker run --rm -v amiquin_amq-data:/data -v $(pwd):/backup alpine \
  tar czf /backup/app_data_backup_$(date +%Y%m%d).tar.gz /data
```

### 3. Recovery Procedures

```bash
# Restore MySQL
docker-compose -f docker-compose.production.yml exec -T mysql mysql \
  -u root -p${AMQ_DB_ROOT_PASSWORD} ${AMQ_DB_NAME} < backup_20240815.sql

# Restore Qdrant
docker run --rm -v amiquin_amq-qdrant-data:/data -v $(pwd):/backup alpine \
  tar xzf /backup/qdrant_backup_20240815.tar.gz -C /

# Restore application data
docker run --rm -v amiquin_amq-data:/data -v $(pwd):/backup alpine \
  tar xzf /backup/app_data_backup_20240815.tar.gz -C /
```

## Performance Optimization

### 1. Database Optimization

```bash
# MySQL performance tuning (already configured in production.cnf)
# - InnoDB buffer pool: 2GB
# - Query cache: 128MB
# - Connection pooling: 300 max connections
# - Binary logging for replication
```

### 2. Qdrant Optimization

```bash
# Vector database performance
# - HNSW index with M=16 for optimal speed/accuracy balance
# - Memory mapping for large datasets
# - WAL optimization for write performance
# - CPU auto-detection for parallel processing
```

### 3. Application Optimization

```bash
# .NET runtime optimization (automatically set)
DOTNET_GCServer=true              # Server GC for better throughput
DOTNET_GCConcurrent=true          # Concurrent garbage collection
DOTNET_GCRetainVM=true            # Retain virtual memory
```

## Scaling Strategies

### Horizontal Scaling

1. **Multiple Bot Instances**:
   ```bash
   # Scale the bot service
   docker-compose -f docker-compose.production.yml up -d --scale amiquinbot=3
   ```

2. **Database Read Replicas**:
   - Set up MySQL read replicas
   - Configure read/write splitting in connection strings

3. **Qdrant Clustering**:
   - Deploy Qdrant cluster for high availability
   - Configure distributed collections

### Vertical Scaling

1. **Increase Resources**:

   ```bash
   # Update resource limits in .env
   QDRANT_MEMORY_LIMIT=16G
   AMIQUIN_MEMORY_LIMIT=8G
   # MySQL tuning: edit mysql/conf.d/production.cnf
   # innodb_buffer_pool_size = 4G
   ```

2. **CPU Optimization**:

   ```bash
   # Optimize for more CPU cores
   QDRANT_MAX_WORKERS=8
   # MySQL tuning: edit mysql/conf.d/production.cnf
   # innodb_read_io_threads = 16
   # innodb_write_io_threads = 16
   ```

## Troubleshooting

### Common Issues

1. **Memory Errors in Qdrant**:
   ```bash
   # Check memory usage
   docker stats qdrant-amiquin-prod
   
   # Increase memory limit
   QDRANT_MEMORY_LIMIT=12G
   ```

2. **MySQL Connection Pool Exhaustion**:

   ```bash
   # Check connections
   docker-compose -f docker-compose.production.yml exec mysql mysql \
     -u root -p -e "SHOW PROCESSLIST;"

   # Increase pool size: edit mysql/conf.d/production.cnf
   # max_connections = 500
   ```

3. **Disk Space Issues**:
   ```bash
   # Check volume usage
   docker system df
   
   # Clean up old logs
   docker-compose -f docker-compose.production.yml exec amiquinbot find /app/Data/Logs -name "*.log" -mtime +7 -delete
   ```

### Health Check Failures

```bash
# Debug service health
docker-compose -f docker-compose.production.yml ps
docker-compose -f docker-compose.production.yml logs [service-name]

# Check individual service endpoints
curl -f http://localhost:6333/health  # Qdrant
curl -f http://localhost/health       # Application
```

## Maintenance

### Regular Tasks

1. **Weekly**:
   - Review logs for errors
   - Check disk space usage
   - Verify backup integrity

2. **Monthly**:
   - Update Docker images
   - Rotate logs manually if needed
   - Performance review

3. **Quarterly**:
   - Security audit
   - Disaster recovery testing
   - Resource utilization review

### Update Procedures

```bash
# 1. Backup before updates
./scripts/backup-all.sh

# 2. Pull latest images
docker-compose -f docker-compose.production.yml pull

# 3. Restart services
docker-compose -f docker-compose.production.yml up -d

# 4. Verify health
curl http://localhost/health
```

This production configuration provides enterprise-grade deployment with high availability, security, and performance optimizations for Amiquin's memory-enabled architecture.