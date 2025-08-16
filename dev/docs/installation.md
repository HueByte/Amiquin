# Amiquin Installation Guide

This comprehensive installation guide covers all deployment methods for Amiquin, from simple Discord bot invitation to complex production deployments.

## 🚀 Quick Start - Discord Server Owners

### Option 1: Hosted Instance (Easiest)

If Hue provides a public hosted instance:

1. **[Invite Amiquin](invite-link)** to your Discord server
2. **Grant permissions** when prompted
3. **Verify installation** with `/help` command
4. **Configure features** using `/configure setup`

**Benefits:**
- ✅ No hosting required
- ✅ Always up-to-date
- ✅ Professional maintenance
- ✅ High availability

**Requirements:**
- Discord server with "Manage Server" permission
- OpenAI API key (for AI features)

## 🐳 Self-Hosting Options

### Option 2: Docker Deployment (Recommended)

Perfect for users who want their own private instance:

#### Simple Docker Run
```bash
# Pull the latest image
docker pull ghcr.io/huebyte/amiquin:latest

# Run with basic configuration
docker run -d \
  --name amiquin \
  --restart unless-stopped \
  -e AMQ_Bot__Token=your_discord_bot_token \
  -e AMQ_LLM__Providers__OpenAI__ApiKey=your_openai_key \
  -e AMQ_Database__Mode=1 \
  -e AMQ_Database__SQLitePath=/app/data/amiquin.db \
  -v amiquin-data:/app/data \
  ghcr.io/huebyte/amiquin:latest
```

#### Docker Compose (Full Stack)
```bash
# Clone the repository
git clone https://github.com/huebyte/amiquin.git
cd amiquin

# Copy and configure environment
cp .env.example .env
nano .env  # Edit with your settings

# Start all services
docker-compose up -d

# Check status
docker-compose ps
```

**What's included:**
- 🤖 Amiquin bot application
- 🗄️ MySQL database
- 🧠 Qdrant vector database (for memory)
- 🔄 Automatic health checks
- 📊 Logging and monitoring

### Option 3: Production Deployment

For enterprise or high-traffic deployments:

```bash
# Use production configuration
docker-compose -f docker-compose.production.yml --profile full up -d

# With SSL and reverse proxy
docker-compose -f docker-compose.production.yml --profile full-proxy up -d
```

**Features:**
- 🔒 SSL/TLS termination with Nginx
- 📈 Resource limits and monitoring
- 🛡️ Security hardening
- 💾 Automatic backups
- ⚡ Performance optimization

## 🛠️ Manual Installation (Advanced)

### Prerequisites

- **.NET 9.0 SDK** - [Download here](https://dotnet.microsoft.com/download)
- **Git** - For source code
- **Database** - MySQL, SQLite, or PostgreSQL
- **Qdrant** - Vector database (optional, for memory features)
- **FFmpeg** - For voice features (optional)

### Step-by-Step Installation

#### 1. Clone Repository
```bash
git clone https://github.com/huebyte/amiquin.git
cd amiquin
```

#### 2. Configure Application
```bash
# Copy configuration template
cp source/Amiquin.Bot/appsettings.example.json source/Amiquin.Bot/appsettings.json

# Copy environment template  
cp .env.example .env

# Edit configuration files
nano source/Amiquin.Bot/appsettings.json
nano .env
```

#### 3. Setup Database
```bash
# For SQLite (default)
# No additional setup needed

# For MySQL
mysql -u root -p
CREATE DATABASE amiquin CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'amiquin'@'%' IDENTIFIED BY 'secure_password';
GRANT ALL PRIVILEGES ON amiquin.* TO 'amiquin'@'%';
FLUSH PRIVILEGES;
```

#### 4. Setup Vector Database (Optional)
```bash
# Start Qdrant with Docker
docker run -p 6333:6333 -p 6334:6334 \
  -v $(pwd)/qdrant_storage:/qdrant/storage:z \
  qdrant/qdrant
```

#### 5. Build and Run
```bash
# Restore dependencies
dotnet restore source/source.sln

# Build application
dotnet build source/source.sln -c Release

# Run database migrations
./scripts/generate-migrations.sh InitialSetup

# Start the bot
dotnet run --project source/Amiquin.Bot -c Release
```

## ⚙️ Configuration Setup

### Discord Bot Creation

1. **Visit [Discord Developer Portal](https://discord.com/developers/applications)**
2. **Create New Application**
   - Click "New Application"
   - Enter name (e.g., "Amiquin")
   - Save changes

3. **Configure Bot**
   - Navigate to "Bot" section
   - Click "Add Bot"
   - Copy bot token
   - Enable necessary intents:
     - ✅ Server Members Intent (if using member features)
     - ✅ Message Content Intent (for message processing)

4. **Set Permissions**
   - Navigate to "OAuth2" > "URL Generator"
   - Select "bot" and "applications.commands" scopes
   - Select required permissions:
     ```
     📝 Send Messages
     📎 Embed Links  
     💬 Use Slash Commands
     📖 Read Message History
     🔧 Manage Messages (for moderation)
     🔊 Connect & Speak (for voice features)
     ```

### OpenAI API Setup

1. **Visit [OpenAI Platform](https://platform.openai.com/)**
2. **Create API Key**
   - Navigate to API Keys section
   - Click "Create new secret key"
   - Copy and securely store the key
3. **Set Usage Limits** (recommended)
   - Set monthly spending limits
   - Configure usage alerts

### Environment Configuration

#### Required Settings
```env
# Discord Configuration (Required)
AMQ_Bot__Token=your_discord_bot_token
AMQ_Bot__Name=amiquin-production

# AI Provider Configuration (Required for AI features)
AMQ_LLM__Providers__OpenAI__ApiKey=sk-your-openai-api-key
AMQ_LLM__Providers__OpenAI__Model=gpt-4

# Database Configuration
AMQ_Database__Mode=0  # 0=MySQL, 1=SQLite, 2=PostgreSQL
AMQ_Database__ConnectionString=Server=localhost;Database=amiquin;User=amiquin;Password=secure_password;

# Memory System (Optional)
AMQ_Memory__Enabled=true
AMQ_Memory__Qdrant__Host=localhost
AMQ_Memory__Qdrant__Port=6334
```

#### Optional Settings
```env
# Logging Configuration
AMQ_Logging__Level=Information
AMQ_Database__LogsPath=/app/data/logs

# Voice Features
AMQ_Voice__PiperPath=/usr/local/bin/piper
AMQ_Voice__DefaultVoice=en_US-lessac-medium

# Performance Tuning
AMQ_Chat__MaxHistoryMessages=50
AMQ_Chat__TokenOptimizationThreshold=3000
```

## 🔍 Verification and Testing

### Post-Installation Checks

#### 1. Service Health
```bash
# Check bot process
ps aux | grep Amiquin

# Check database connection
docker-compose exec mysql mysqladmin ping

# Check Qdrant status (if enabled)
curl http://localhost:6333/health
```

#### 2. Discord Integration
```bash
# Test in Discord
/help                 # Should show command list
/status               # Should show bot status
/chat message: hello  # Should respond with AI
```

#### 3. Log Analysis
```bash
# View recent logs
tail -f /app/data/logs/amiquin-*.log

# Check for errors
grep -i error /app/data/logs/amiquin-*.log
```

## 🔧 Troubleshooting

### Common Issues

#### Bot Appears Offline
**Symptoms:** Bot shows as offline in Discord
**Solutions:**
1. Verify Discord token is correct
2. Check network connectivity
3. Review bot logs for authentication errors
4. Ensure bot has proper permissions

#### Commands Not Working
**Symptoms:** Slash commands don't appear or fail
**Solutions:**
1. Verify bot has "Use Slash Commands" permission
2. Check if commands are registered (logs show registration)
3. Try reinviting bot with fresh permissions
4. Clear Discord cache

#### AI Features Not Responding
**Symptoms:** `/chat` command fails or gives errors
**Solutions:**
1. Verify OpenAI API key is valid
2. Check OpenAI account has available credits
3. Review rate limiting in logs
4. Test with simpler prompts

#### Memory System Issues
**Symptoms:** Memory features not working
**Solutions:**
1. Verify Qdrant is running (`curl http://localhost:6333/health`)
2. Check memory configuration in settings
3. Verify database collections are created
4. Review Qdrant logs for errors

#### Database Connection Errors
**Symptoms:** Bot fails to start or store data
**Solutions:**
1. Verify database server is running
2. Check connection string format
3. Ensure database user has proper permissions
4. Run database migrations manually

#### Performance Issues
**Symptoms:** Slow responses or timeouts
**Solutions:**
1. Check system resources (CPU, memory)
2. Optimize database queries
3. Adjust token limits and history size
4. Scale to more powerful hardware

### Diagnostic Commands

```bash
# Check all Docker services
docker-compose ps

# View service logs
docker-compose logs -f amiquin

# Check resource usage
docker stats

# Test database connection
docker-compose exec mysql mysql -u amiquin -p -e "SELECT 1;"

# Test Qdrant connection
curl -X GET "http://localhost:6333/collections"
```

## 📊 Monitoring and Maintenance

### Health Monitoring

#### Built-in Health Checks
- **Application Health**: `/health` endpoint
- **Database Health**: Automatic connection monitoring  
- **AI Provider Health**: API status checking
- **Memory System Health**: Qdrant connectivity

#### External Monitoring
```bash
# Setup monitoring with Docker health checks
docker-compose exec amiquin curl -f http://localhost:5000/health

# Monitor with external tools
# - Prometheus/Grafana
# - Uptime Robot
# - Custom scripts
```

### Regular Maintenance

#### Daily Tasks
- Review error logs
- Check resource usage
- Monitor AI API costs

#### Weekly Tasks
- Update Docker images
- Backup database
- Review performance metrics

#### Monthly Tasks
- Security updates
- Configuration review
- Capacity planning

## 🆙 Updating Amiquin

### Docker Update Process
```bash
# Pull latest image
docker-compose pull

# Recreate containers
docker-compose up -d

# Verify update
docker-compose logs -f amiquin
```

### Manual Update Process
```bash
# Backup current installation
cp -r source source-backup

# Pull latest changes
git fetch origin
git pull origin main

# Update dependencies
dotnet restore source/source.sln

# Build new version
dotnet build source/source.sln -c Release

# Run any new migrations
./scripts/generate-migrations.sh

# Restart application
systemctl restart amiquin
```

## 🆘 Getting Help

### Support Channels

1. **Documentation** - This guide and technical references
2. **GitHub Issues** - Bug reports and feature requests
3. **Community Discord** - Real-time help and discussion
4. **Developer Support** - Direct assistance for complex issues

### Before Requesting Help

1. **Check logs** for error messages
2. **Verify configuration** against examples
3. **Test with minimal setup** to isolate issues
4. **Gather system information** (OS, Docker version, etc.)

### Information to Include

When requesting help, provide:
- Amiquin version
- Deployment method (Docker/manual)
- Operating system
- Error messages/logs
- Configuration (redacted sensitive info)
- Steps to reproduce issue

---

**Ready to deploy?** Choose your installation method and follow the steps above. For ongoing support, refer to the [Configuration Guide](configuration.md) and [User Guide](user-guide.md).