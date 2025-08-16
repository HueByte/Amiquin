# Configuration Guide

This comprehensive guide covers how to configure Amiquin for your Discord server, including environment variables, AI providers, memory system, and advanced features.

## Quick Start Configuration

### Environment Variables (AMQ_ Prefix)

Amiquin uses a unified environment variable system with the `AMQ_` prefix. Create a `.env` file from the example:

```bash
# Copy the example configuration
cp .env.example .env

# Edit with your settings
nano .env  # or your preferred editor
```

### Essential Configuration

```env
# Discord Bot Token (Required)
AMQ_Bot__Token=your-discord-bot-token-here

# AI Provider (Required for chat features)
AMQ_LLM__Providers__OpenAI__ApiKey=sk-your-openai-api-key-here

# Database Mode (Required)
AMQ_Database__Mode=1  # 1=SQLite (default), 0=MySQL
```

## Configuration Methods

Amiquin supports multiple configuration methods with this priority order:

1. **Environment Variables** (`.env` file) - **Recommended**
2. **Configuration Files** (`appsettings.json`)
3. **Docker Environment** (for containerized deployments)
4. **System Environment Variables**

### Environment Variable Format

All Amiquin environment variables use the `AMQ_` prefix with double underscore `__` for nested properties:

```env
# Format: AMQ_Section__SubSection__Property=value
AMQ_Bot__Token=your-token
AMQ_LLM__Providers__OpenAI__ApiKey=your-key
AMQ_Memory__Qdrant__Host=localhost
```

## Core Configuration Sections

### Bot Configuration

```env
# Bot Identity and Behavior
AMQ_Bot__Token=your-discord-bot-token-here
AMQ_Bot__Name=Amiquin
AMQ_Bot__PrintLogo=false
AMQ_Bot__MessageFetchCount=40
```

### Database Configuration

#### SQLite (Recommended for Development)
```env
AMQ_Database__Mode=1
AMQ_ConnectionStrings__Amiquin-Sqlite=Data Source=Data/Database/amiquin.db
```

#### MySQL (Recommended for Production)
```env
AMQ_Database__Mode=0
AMQ_ConnectionStrings__Amiquin-Mysql="Server=localhost;Port=3306;Database=amiquin_db;Uid=amiquin_user;Pwd=your_password;SslMode=None;AllowPublicKeyRetrieval=True;Pooling=True;"
```

### AI/LLM Provider Configuration

#### OpenAI (Primary Provider)
```env
AMQ_LLM__DefaultProvider=OpenAI
AMQ_LLM__Providers__OpenAI__Enabled=true
AMQ_LLM__Providers__OpenAI__ApiKey=sk-your-openai-api-key-here
AMQ_LLM__Providers__OpenAI__BaseUrl=https://api.openai.com/v1/
AMQ_LLM__Providers__OpenAI__DefaultModel=gpt-4o-mini
```

#### Grok (Optional Secondary Provider)
```env
AMQ_LLM__Providers__Grok__Enabled=false
AMQ_LLM__Providers__Grok__ApiKey=xai-your-grok-api-key-here
AMQ_LLM__Providers__Grok__BaseUrl=https://api.x.ai/v1/
AMQ_LLM__Providers__Grok__DefaultModel=grok-3
```

#### Gemini (Optional Secondary Provider)
```env
AMQ_LLM__Providers__Gemini__Enabled=false
AMQ_LLM__Providers__Gemini__ApiKey=your-gemini-api-key-here
AMQ_LLM__Providers__Gemini__BaseUrl=https://generativelanguage.googleapis.com/
AMQ_LLM__Providers__Gemini__DefaultModel=gemini-1.5-flash
```

### Memory System Configuration

The memory system enables long-term conversation context using a vector database.

#### Basic Memory Setup
```env
# Enable Memory System
AMQ_Memory__Enabled=true
AMQ_Memory__EmbeddingModel=text-embedding-3-small
AMQ_Memory__MaxMemoriesPerSession=1000
AMQ_Memory__MaxContextMemories=10
AMQ_Memory__SimilarityThreshold=0.7
```

#### Qdrant Vector Database
```env
# Local Qdrant Instance
AMQ_Memory__Qdrant__Host=localhost
AMQ_Memory__Qdrant__Port=6334
AMQ_Memory__Qdrant__UseHttps=false
AMQ_Memory__Qdrant__CollectionName=amiquin_memories
AMQ_Memory__Qdrant__VectorSize=1536
AMQ_Memory__Qdrant__Distance=Cosine
AMQ_Memory__Qdrant__AutoCreateCollection=true

# For Docker Compose (use service name)
# AMQ_Memory__Qdrant__Host=qdrant

# For Cloud Qdrant (optional)
# AMQ_Memory__Qdrant__ApiKey=your-qdrant-cloud-api-key
```

### Voice/TTS Configuration

```env
# Enable Voice Features
AMQ_Voice__Enabled=true
AMQ_Voice__PiperCommand=/usr/local/bin/piper
AMQ_Voice__TTSModelName=en_GB-northern_english_male-medium
```

### Data Paths Configuration

```env
AMQ_DataPaths__Logs=Data/Logs
AMQ_DataPaths__Messages=Data/Messages
AMQ_DataPaths__Sessions=Data/Sessions
AMQ_DataPaths__Plugins=Data/Plugins
AMQ_DataPaths__Configuration=Configuration
```

### Logging Configuration

```env
# Serilog Logging Levels
AMQ_Serilog__MinimumLevel__Default=Information
AMQ_Serilog__MinimumLevel__Override__System=Warning
AMQ_Serilog__MinimumLevel__Override__Microsoft=Warning
AMQ_Serilog__MinimumLevel__Override__Discord=Information
```

### Session Management

```env
# Chat Session Configuration
AMQ_SessionManagement__CleanupIntervalMinutes=30
AMQ_SessionManagement__InactivityTimeoutMinutes=120
AMQ_SessionManagement__MaxHistoryLength=50
AMQ_SessionManagement__MaxSessionsPerUser=5
```

## Docker Configuration

For Docker deployments, additional configuration is available:

### Docker Compose Variables

```env
# Docker Service Names
AMQ_BOT_NAME=amiquin-instance

# MySQL for Docker Compose
AMQ_DB_ROOT_PASSWORD=your-secure-root-password-here
AMQ_DB_NAME=amiquin_db
AMQ_DB_USER=amiquin_user
AMQ_DB_USER_PASSWORD=your-secure-user-password-here

# Qdrant Docker Configuration
AMQ_QDRANT_HTTP_PORT=6333
AMQ_QDRANT_GRPC_PORT=6334
AMQ_QDRANT_WEB_UI_PORT=3000
AMQ_QDRANT_LOG_LEVEL=INFO
AMQ_QDRANT_WEB_UI_ENABLED=false
```

### Host-Specific Configuration

#### Local Development
```env
# Use localhost for local services
AMQ_Memory__Qdrant__Host=localhost
AMQ_ConnectionStrings__Amiquin-Mysql="Server=localhost;Port=3306;Database=amiquin_db;Uid=amiquin_user;Pwd=your_password;"
```

#### Docker Compose Deployment
```env
# Use service names for Docker networking
AMQ_Memory__Qdrant__Host=qdrant
AMQ_ConnectionStrings__Amiquin-Mysql="Server=mysql;Port=3306;Database=${AMQ_DB_NAME};Uid=${AMQ_DB_USER};Pwd=${AMQ_DB_USER_PASSWORD};"
```

## Configuration Validation

### Required Settings Checklist

✅ **Essential Settings**:
- `AMQ_Bot__Token` - Discord bot token
- `AMQ_LLM__Providers__OpenAI__ApiKey` - OpenAI API key (for AI features)
- `AMQ_Database__Mode` - Database provider selection

✅ **Memory System** (if enabled):
- `AMQ_Memory__Enabled=true`
- `AMQ_Memory__Qdrant__Host` - Qdrant database host
- `AMQ_Memory__Qdrant__Port` - Qdrant database port

✅ **Production Settings**:
- Secure database passwords
- Proper logging configuration
- Performance tuning parameters

### Configuration Testing

```bash
# Test configuration
dotnet run --project source/Amiquin.Bot --configuration Release

# Check logs for configuration issues
tail -f Data/Logs/amiquin-*.log
```

### Advanced Model Configuration

#### Custom Model Settings
```env
# OpenAI Model Customization
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__Name=GPT-4 Omni
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__MaxTokens=128000
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__MaxOutputTokens=4096
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__Temperature=0.7

# Global LLM Settings
AMQ_LLM__GlobalSystemMessage="I want you to act as personal assistant called Amiquin. You are friendly, helpful and professional."
AMQ_LLM__GlobalTemperature=0.6
AMQ_LLM__GlobalTimeout=120
AMQ_LLM__EnableFallback=true
```

### Content Scrapper Configuration (Optional)
```env
# NSFW Content Scrapper Settings
AMQ_Scrappers__CacheExpirationMinutes=60
AMQ_Scrappers__CacheSize=200
# Individual provider configurations are complex and defined in appsettings.json
```

## Troubleshooting Configuration

### Common Configuration Issues

#### Bot Won't Start
**Check**:
- Discord token validity (`AMQ_Bot__Token`)
- Database connection string format
- File permissions for SQLite database
- Network connectivity for external services

#### AI Features Not Working
**Check**:
- OpenAI API key validity and format
- API key permissions and rate limits
- LLM provider configuration
- Internet connectivity to AI services

#### Memory System Issues
**Check**:
- Qdrant service is running and accessible
- Correct host/port configuration
- Network connectivity to Qdrant
- Collection creation permissions

#### Database Connection Errors
**Check**:
- Database service is running
- Connection string format
- User permissions on database
- Network connectivity to database server

### Configuration Validation Commands

```bash
# Test database connection
dotnet ef database update --project source/Amiquin.Infrastructure --startup-project source/Amiquin.Bot

# Test Qdrant connection
curl http://localhost:6333/health

# Validate configuration
dotnet run --project source/Amiquin.Bot --verify-config
```

### Configuration Security

#### Best Practices
1. **Never commit sensitive data** to version control
2. **Use strong passwords** for database accounts
3. **Restrict API key permissions** where possible
4. **Use environment-specific configurations**
5. **Regularly rotate API keys and passwords**

#### Sensitive Information
Always keep these values secret:
- Discord bot tokens
- OpenAI/AI provider API keys
- Database passwords
- API keys for external services

### Getting Configuration Help

1. **Check configuration examples** in `.env.example`
2. **Review logs** in Data/Logs/ directory
3. **Validate settings** using built-in commands
4. **GitHub Issues** for configuration problems
5. **Documentation** for detailed explanations

## Configuration Migration

### Version Upgrades

When upgrading Amiquin versions:

1. **Backup current configuration**
2. **Review changelog** for configuration changes
3. **Update environment variables** as needed
4. **Test new configuration** in development
5. **Migrate production** after validation

### Backup and Restore

#### Configuration Backup
```bash
# Backup environment file
cp .env .env.backup

# Backup database
cp -r Data/Database Data/Database.backup

# Backup logs (optional)
cp -r Data/Logs Data/Logs.backup
```

#### Configuration Restore
```bash
# Restore environment
cp .env.backup .env

# Restore database
cp -r Data/Database.backup Data/Database
```

## Performance Configuration

### Memory System Optimization

```env
# Memory Performance Tuning
AMQ_Memory__MaxMemoriesPerSession=1000
AMQ_Memory__MaxContextMemories=10
AMQ_Memory__SimilarityThreshold=0.7
AMQ_Memory__MinImportanceScore=0.3
AMQ_Memory__AutoCleanup=true
AMQ_Memory__CleanupOlderThanDays=30
```

### Database Performance

```env
# Connection Pooling
AMQ_ConnectionStrings__Amiquin-Mysql="Server=localhost;Database=amiquin;Pooling=True;MinimumPoolSize=5;MaximumPoolSize=100;"

# Logging Performance
AMQ_Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command=Warning
```

### AI Provider Optimization

```env
# Request Timeouts
AMQ_LLM__GlobalTimeout=120

# Token Limits
AMQ_Bot__MaxTokens=20000
AMQ_Bot__MessageFetchCount=40
```

---

**Next Steps**: 
- [Installation Guide](installation.md) for deployment instructions
- [User Guide](user-guide.md) for feature documentation
- [Docker Setup](docker-setup.md) for containerized deployment