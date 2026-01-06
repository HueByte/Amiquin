# 🚀 Amiquin Deployment Guide

This guide covers deploying Amiquin to production environments.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Quick Start](#quick-start)
3. [Pre-Deployment Checklist](#pre-deployment-checklist)
4. [Configuration](#configuration)
5. [Deployment Methods](#deployment-methods)
6. [Production Best Practices](#production-best-practices)
7. [Monitoring & Maintenance](#monitoring--maintenance)
8. [Troubleshooting](#troubleshooting)

## Prerequisites

### Required

- **Discord Bot Token** - From [Discord Developer Portal](https://discord.com/developers/applications)
- **LLM Provider API Key** - Choose one or more:
  - **OpenAI** - From [OpenAI Platform](https://platform.openai.com/api-keys)
  - **Grok (xAI)** - From [xAI Console](https://console.x.ai/)
  - **Gemini** - From [Google AI Studio](https://aistudio.google.com/)

### For Docker Deployment

- **Docker & Docker Compose** - Container orchestration (handles MySQL, Qdrant automatically)

### For Local Development

- **.NET SDK 10.0+** - For building and running the application
- **MySQL 8.0+** - Optional production database (SQLite used by default)
- **Qdrant** - Optional vector database for AI memory system

### Optional Features

- **ffmpeg** - For voice/TTS features
- **Piper** - Text-to-speech engine

## Quick Start

### 1. Initial Setup

Clone the repository and run the setup script:

**Windows (PowerShell):**

```powershell
git clone https://github.com/HueByte/Amiquin.git
cd Amiquin
./scripts/setup-project.ps1 -Production
```

**Linux/macOS:**

```bash
git clone https://github.com/HueByte/Amiquin.git
cd Amiquin
chmod +x scripts/*.sh
./scripts/setup-project.sh
```

The setup script will:

- Create `.env` configuration file
- Generate secure passwords for MySQL
- Prompt for API keys
- Configure optional features
- Build the solution

### 2. Run Pre-Deployment Checks

Validate your configuration before deploying:

**Windows:**

```powershell
./scripts/pre-deployment-checklist.ps1 -Production
```

**Linux/macOS:**

```bash
./scripts/pre-deployment-checklist.sh --production
```

### 3. Deploy

**Docker Compose (Recommended):**

```bash
# Production deployment with MySQL and Qdrant
docker-compose --profile prod up -d

# View logs
docker-compose logs -f amiquinbot

# Stop services
docker-compose --profile prod down
```

> **Important for Docker users:** When using Docker Compose, your `.env` file must use service names (`mysql`, `qdrant`) instead of `localhost` for database and memory system connections. The setup script configures this automatically.

**Standalone .NET:**

```bash
cd source/Amiquin.Bot
dotnet run -c Release
```

## Pre-Deployment Checklist

Before deploying to production, ensure:

### ✅ Configuration

- [ ] `.env` file exists with all required values
- [ ] Discord bot token configured
- [ ] At least one LLM provider API key configured (OpenAI, Grok, or Gemini)
- [ ] Database connection string set (if using MySQL)
- [ ] Strong passwords generated (16+ characters)
- [ ] `.env` is in `.gitignore` (not committed to git)

### ✅ Dependencies

- [ ] Docker and Docker Compose installed
- [ ] .NET SDK 9.0+ installed
- [ ] MySQL container running (if using MySQL database)
- [ ] Qdrant container running (if memory system enabled)

### ✅ Project Structure

- [ ] All data directories exist (`Data/Database`, `Data/Logs`, etc.)
- [ ] Solution builds without errors
- [ ] All tests pass

### ✅ Security (Production)

- [ ] No default/weak passwords in configuration
- [ ] Qdrant authentication enabled (if exposed)
- [ ] Appropriate logging level set (Warning or Error)
- [ ] Secrets not in version control

### ✅ Optional Features

- [ ] Web search configured (if enabled)
- [ ] Voice/TTS dependencies installed (if enabled)
- [ ] Memory system tested (if enabled)

## Configuration

### Required Environment Variables

```bash
# Discord
AMQ_Discord__Token="your-discord-bot-token"

# AI/LLM (at least one required)
AMQ_LLM__Providers__OpenAI__ApiKey="sk-your-openai-api-key"
# AMQ_LLM__Providers__Grok__ApiKey="xai-your-grok-api-key"
# AMQ_LLM__Providers__Gemini__ApiKey="your-gemini-api-key"
```

### Database Configuration

**SQLite (Default - Development):**

```bash
AMQ_Database__Mode=1
# Connection string is auto-configured for SQLite
```

**MySQL (Production):**

For **Docker Compose** deployment:

```bash
AMQ_Database__Mode=0
# Use Docker service name 'mysql' when running in containers
AMQ_Database__ConnectionString="Server=mysql;Port=3306;Database=amiquin_db;Uid=amiquin_user;Pwd=your_secure_password;SslMode=None;AllowPublicKeyRetrieval=True;Pooling=True;"

# Docker Compose environment variables
AMQ_DB_ROOT_PASSWORD="your_secure_root_password"
AMQ_DB_USER_PASSWORD="your_secure_user_password"
AMQ_DB_NAME="amiquin_db"
AMQ_DB_USER="amiquin_user"
```

For **local MySQL** (outside Docker):

```bash
AMQ_Database__Mode=0
# Use 'localhost' when MySQL is running locally
AMQ_Database__ConnectionString="Server=localhost;Port=3306;Database=amiquin_db;Uid=amiquin_user;Pwd=your_secure_password;Pooling=True;"
```

> **⚠️ Important:** The key difference is `Server=mysql` for Docker vs `Server=localhost` for local MySQL.

### Memory System (Qdrant)

For **Docker Compose** deployment:

```bash
AMQ_Memory__Enabled=true
AMQ_Memory__Qdrant__Host=qdrant  # Docker service name
AMQ_Memory__Qdrant__Port=6334
# AMQ_Memory__Qdrant__ApiKey="your-api-key"  # Optional for cloud instances
```

For **local Qdrant** (outside Docker):

```bash
AMQ_Memory__Enabled=true
AMQ_Memory__Qdrant__Host=localhost  # Local installation
AMQ_Memory__Qdrant__Port=6334
```

### Web Search

```bash
AMQ_WebSearch__Enabled=true
AMQ_WebSearch__Provider=DuckDuckGo  # Or: Google, Bing

# For Google Custom Search
# AMQ_WebSearch__ApiKey="your-google-api-key"
# AMQ_WebSearch__SearchEngineId="your-search-engine-id"

# For Bing Search API
# AMQ_WebSearch__ApiKey="your-bing-api-key"
```

## Deployment Methods

### Method 1: Docker Compose (Recommended)

**Development:**

```bash
docker-compose --profile dev up -d
```

**Production:**

```bash
docker-compose --profile prod up -d
```

**With Monitoring:**

```bash
docker-compose --profile prod-full up -d
```

### Method 2: Automated Deployment Script

**Windows:**

```powershell
./scripts/deploy-production.ps1 `
    -DatabaseType mysql `
    -Environment production `
    -InstanceName amiquin-prod `
    -EnableMonitoring `
    -EnableBackups
```

**Linux/macOS:**

```bash
./scripts/deploy-production.sh \
    --database mysql \
    --environment production \
    --instance amiquin-prod
```

### Method 3: Manual Deployment

1. **Build the application:**

```bash
dotnet publish source/Amiquin.Bot -c Release -o ./publish
```

1. **Copy files to server:**

```bash
scp -r ./publish user@server:/opt/amiquin/
scp .env user@server:/opt/amiquin/
```

1. **Run on server:**

```bash
cd /opt/amiquin
./Amiquin.Bot
```

### Method 4: Systemd Service (Linux)

Create `/etc/systemd/system/amiquin.service`:

```ini
[Unit]
Description=Amiquin Discord Bot
After=network.target

[Service]
Type=simple
User=amiquin
WorkingDirectory=/opt/amiquin
ExecStart=/opt/amiquin/Amiquin.Bot
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Enable and start:

```bash
sudo systemctl enable amiquin
sudo systemctl start amiquin
sudo systemctl status amiquin
```

## Production Best Practices

### Security

1. **Use Strong Passwords:**
   - Generate secure passwords for MySQL, Qdrant
   - Minimum 16 characters with mixed case, numbers, symbols
   - Use password managers or the setup script generator

2. **Secure API Keys:**
   - Never commit `.env` to version control
   - Use environment variables or Docker secrets
   - Rotate keys regularly

3. **Enable Authentication:**

   ```bash
   # Qdrant authentication for production
   AMQ_QDRANT_API_KEY="your-secure-api-key"
   AMQ_Memory__Qdrant__ApiKey="your-secure-api-key"
   ```

4. **Network Security:**
   - Use private Docker networks
   - Expose only necessary ports
   - Enable firewall rules

### Performance

1. **Database Optimization:**
   - Use MySQL for production (better performance at scale)
   - Enable connection pooling
   - Regular database maintenance

2. **Memory Management:**
   - Configure appropriate memory limits for containers
   - Monitor memory usage
   - Enable automatic cleanup:

     ```bash
     AMQ_Memory__AutoCleanup=true
     AMQ_Memory__CleanupOlderThanDays=30
     ```

3. **Caching:**
   - Web search caching enabled by default
   - Adjust cache expiration as needed:

     ```bash
     AMQ_WebSearch__EnableCaching=true
     AMQ_WebSearch__CacheExpirationMinutes=30
     ```

### Reliability

1. **Container Restart Policies:**

   ```yaml
   # In docker-compose.yml
   restart: unless-stopped
   ```

2. **Health Checks:**
   - MySQL and Qdrant containers have built-in health checks
   - Monitor with Docker health status

3. **Logging:**

   ```bash
   # Production logging
   AMQ_Serilog__MinimumLevel__Default=Warning
   
   # Log retention
   docker-compose logs --tail=100 --follow amiquin-bot
   ```

4. **Backups:**
   - Regular database backups
   - Configuration file backups
   - Automated backup scripts available

## Monitoring & Maintenance

### Container Management

**View running containers:**

```bash
docker-compose ps
```

**View logs:**

```bash
docker-compose logs -f amiquin-bot
docker-compose logs --tail=100 mysql
docker-compose logs --tail=100 qdrant
```

**Restart services:**

```bash
docker-compose restart amiquin-bot
```

**Update containers:**

```bash
docker-compose pull
docker-compose up -d
```

### Database Maintenance

**Backup MySQL:**

```bash
docker exec mysql-amiquin mysqldump -u root -p amiquin_db > backup.sql
```

**Restore MySQL:**

```bash
docker exec -i mysql-amiquin mysql -u root -p amiquin_db < backup.sql
```

**Backup Qdrant:**

```bash
docker exec qdrant-amiquin tar czf /qdrant/snapshots/backup.tar.gz /qdrant/storage
```

### Performance Monitoring

**Docker stats:**

```bash
docker stats
```

**Container resource usage:**

```bash
docker-compose top
```

**Application logs:**

```bash
tail -f Data/Logs/amiquin-*.log
```

## Troubleshooting

### Bot Not Starting

1. **Check logs:**

   ```bash
   docker-compose logs amiquin-bot
   ```

2. **Verify configuration:**

   ```powershell
   ./scripts/pre-deployment-checklist.ps1
   ```

3. **Common issues:**
   - Missing Discord token or API keys
   - Database connection failure
   - Port conflicts

### Database Connection Issues

1. **Check MySQL container:**

   ```bash
   docker-compose ps mysql
   docker-compose logs mysql
   ```

2. **Verify credentials:**
   - Check `.env` file passwords
   - Ensure connection string is correct

3. **Test connection:**

   ```bash
   docker exec mysql-amiquin mysql -u amiquin_user -p -e "SELECT 1"
   ```

### Memory System Not Working

1. **Check Qdrant container:**

   ```bash
   docker-compose ps qdrant
   docker-compose logs qdrant
   ```

2. **Verify configuration:**

   ```bash
   # In Docker, use service name as host
   AMQ_Memory__Qdrant__Host=qdrant
   ```

3. **Test connection:**

   ```bash
   curl http://localhost:6333/health
   ```

### Web Search Failing

1. **Check provider:**
   - DuckDuckGo: No API key required
   - Google: Requires API key + Search Engine ID
   - Bing: Requires API key

2. **Verify API keys:**
   - Check rate limits
   - Validate key permissions
   - Test keys externally

3. **Check logs:**

   ```bash
   grep "WebSearch" Data/Logs/amiquin-*.log
   ```

### Performance Issues

1. **Check resource usage:**

   ```bash
   docker stats
   ```

2. **Optimize configuration:**
   - Reduce conversation token limit
   - Enable memory cleanup
   - Adjust cache expiration

3. **Database optimization:**
   - Enable query caching
   - Optimize indexes
   - Regular VACUUM (SQLite) or OPTIMIZE (MySQL)

## Additional Resources

- [GitHub Pages Documentation](https://huebyte.github.io/Amiquin/)
- [Docker Compose Reference](https://docs.docker.com/compose/)
- [Discord.NET Documentation](https://docs.discordnet.dev/)
- [OpenAI API Documentation](https://platform.openai.com/docs/)

## Support

For issues and questions:

- GitHub Issues: [Create an issue](https://github.com/HueByte/Amiquin/issues)
- Documentation: [GitHub Pages](https://huebyte.github.io/Amiquin/)
