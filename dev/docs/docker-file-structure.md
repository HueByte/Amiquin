# Docker File Structure and Volume Mapping

This document explains how files and directories are organized in the Amiquin Docker deployment, including volume mappings and data persistence.

## Container File Structure

### Application Directory

```text
/home/app/amiquin/build/  # Main application directory
├── Amiquin.Bot.dll      # Main application executable
├── appsettings.json     # Runtime configuration (if not using volume)
├── opus.dll            # Audio codec library
├── libsodium.dll       # Cryptography library
└── Data/               # Data directory (linked to volumes)
```

### Volume Mount Points

```text
/app/Data/               # Main data volume mount point
├── Configuration/       # Configuration files
│   ├── appsettings.json # Main configuration file
│   └── appsettings.example.json
├── Logs/               # Application logs
│   └── amiquin-.log    # Rolling log files (daily rotation)
├── Messages/           # Message templates and system messages
│   ├── System.md       # AI system personality/instructions
│   ├── ServerJoinMessage.md
│   └── *.md           # Other message templates
├── Sessions/           # Chat session data
│   └── *.json         # Session state files
└── Plugins/           # Plugin directory (future use)
    └── *.dll          # Plugin assemblies
```

## Volume Mappings

### docker-compose.yml Volume Configuration

```yaml
volumes:
  - amq-data:/app/Data                    # Main data volume
  - amq-logs:/app/Data/Logs              # Logs volume
  - amq-messages:/app/Data/Messages       # Messages volume  
  - amq-sessions:/app/Data/Sessions       # Sessions volume
  - amq-plugins:/app/Data/Plugins         # Plugins volume
```

### Host to Container Mapping

| Host Volume | Container Path | Purpose |
|-------------|----------------|---------|
| `amq-data` | `/app/Data/` | Main data directory |
| `amq-logs` | `/app/Data/Logs/` | Application logs |
| `amq-messages` | `/app/Data/Messages/` | Message templates |
| `amq-sessions` | `/app/Data/Sessions/` | Chat session data |
| `amq-plugins` | `/app/Data/Plugins/` | Plugin files |

## Configuration File Hierarchy

### Runtime Configuration Resolution

The application looks for configuration in this order:

1. **Environment Variable Path**: `AMQ_APPSETTINGS_PATH`
   - Default: `/app/Data/Configuration/appsettings.json`
   - Can be overridden in docker-compose.yml or .env file

2. **Fallback Path**: `{AppDirectory}/Data/Configuration/appsettings.json`
   - Used if environment variable not set

3. **Build-time Fallback**: If no appsettings.json exists, copies from appsettings.example.json

### Configuration File Locations

```text
Container Paths:
├── /app/Data/Configuration/appsettings.json     # Primary config (volume-mounted)
├── /app/Data/Configuration/appsettings.example.json # Template config
└── /home/app/amiquin/build/appsettings.json    # Fallback (not recommended)
```

## Data Persistence

### Persistent Data

The following data persists across container restarts via volumes:

- **Configuration**: appsettings.json, message templates
- **Logs**: Application logs with 7-day retention
- **Sessions**: Chat session state and history
- **Database**: SQLite database files (when using SQLite mode)
- **Messages**: AI personality files and templates

### Temporary Data

The following data does NOT persist:

- **Application binaries**: Rebuilt with each deployment
- **Temp files**: Cleared on container restart
- **Memory cache**: Reset on restart

## Environment Variables

### Path Configuration

```bash
# Main configuration file path
AMQ_APPSETTINGS_PATH=/app/Data/Configuration/appsettings.json

# Data directory paths
AMQ_DataPaths__Logs=/app/Data/Logs
AMQ_DataPaths__Messages=/app/Data/Messages
AMQ_DataPaths__Sessions=/app/Data/Sessions
AMQ_DataPaths__Plugins=/app/Data/Plugins
AMQ_DataPaths__Configuration=/app/Data/Configuration
```

### Database Paths

```bash
# SQLite database path (when using SQLite mode)
AMQ_ConnectionStrings__AmiquinContext="Data Source=/app/Data/Database/amiquin.db"

# Logs path for Serilog
AMQ_DataPaths__Database=/app/Data/Database
```

## File Permissions

### Container User

- **User**: `amiquin` (UID: 1000)
- **Group**: `amiquin` (GID: 1000)
- **Home**: `/home/amiquin`

### Directory Permissions

```bash
/app/Data/           # Owner: amiquin:amiquin, Mode: 755
├── Configuration/   # Owner: amiquin:amiquin, Mode: 755
├── Logs/           # Owner: amiquin:amiquin, Mode: 755 
├── Messages/       # Owner: amiquin:amiquin, Mode: 755
├── Sessions/       # Owner: amiquin:amiquin, Mode: 755
└── Plugins/        # Owner: amiquin:amiquin, Mode: 755
```

## Log Files

### Serilog Configuration

```json
{
  "Serilog": {
    "WriteTo": [{
      "Name": "File",
      "Args": {
        "path": "/app/Data/Logs/amiquin-.log",
        "rollingInterval": "Day",
        "retainedFileCountLimit": 7
      }
    }]
  }
}
```

### Log File Pattern

```text
/app/Data/Logs/
├── amiquin-20250821.log  # Today's log
├── amiquin-20250820.log  # Yesterday's log
└── amiquin-*.log         # Previous days (up to 7 days)
```

## Database Files

### SQLite Mode (Database:Mode = 1)

```text
/app/Data/Database/
└── amiquin.db           # SQLite database file
```

### MySQL Mode (Database:Mode = 0)

- Database runs in separate `mysql` container
- No local database files in app container
- Connection via container networking: `Server=mysql;Port=3306`

## Message Templates

### System Message Files

```text
/app/Data/Messages/
├── System.md                # Main AI personality (renamed from Persona.md)
├── ServerJoinMessage.md     # Welcome message template
├── Persona.example.md       # Example personality template
└── Persona_v1.md           # Legacy personality file
```

### Message Template Usage

- **System.md**: Loaded as AI system prompt for chat interactions
- **ServerJoinMessage.md**: Used for Discord server join messages
- Templates support variable substitution and markdown formatting

## Backup and Migration

### Backup Strategy

To backup Amiquin data:

```bash
# Backup all data volumes
docker-compose down
docker run --rm -v amq-data:/data -v $(pwd):/backup ubuntu tar czf /backup/amiquin-data-backup.tar.gz /data

# Backup specific volumes
docker run --rm -v amq-logs:/data -v $(pwd):/backup ubuntu tar czf /backup/logs-backup.tar.gz /data
```

### Migration Between Hosts

```bash
# Export volumes on source host
docker run --rm -v amq-data:/data -v $(pwd):/backup ubuntu tar czf /backup/data.tar.gz /data

# Import on destination host  
docker run --rm -v amq-data:/data -v $(pwd):/backup ubuntu tar xzf /backup/data.tar.gz
```

## Troubleshooting

### Common Path Issues

1. **Configuration not found**: Check `AMQ_APPSETTINGS_PATH` environment variable
2. **Permission denied**: Ensure volumes owned by UID 1000 (amiquin user)
3. **Logs not persisting**: Verify `amq-logs` volume is properly mounted
4. **Database not found**: Check SQLite path and volume mounting

### Volume Inspection

```bash
# List all volumes
docker volume ls | grep amq

# Inspect volume mount points
docker volume inspect amq-data

# Check volume contents
docker run --rm -v amq-data:/data ubuntu ls -la /data
```

### Path Verification

```bash
# Check container paths
docker exec -it bot-amiquin ls -la /app/Data/

# Verify configuration path
docker exec -it bot-amiquin cat /app/Data/Configuration/appsettings.json

# Check log files
docker exec -it bot-amiquin ls -la /app/Data/Logs/
```

## Best Practices

### Production Deployment

1. **Use named volumes**: Avoid bind mounts for production data
2. **Regular backups**: Implement automated backup strategy for volumes
3. **Monitor disk usage**: Set up alerts for volume space usage
4. **Separate sensitive config**: Use environment variables for secrets
5. **Log rotation**: Configure appropriate log retention policies

### Development

1. **Use bind mounts**: For easier access to logs and config during development
2. **Volume inspection**: Regularly check volume contents during debugging
3. **Configuration testing**: Test different config paths and environment variables
4. **Clean deployment**: Remove old volumes when testing significant changes

This file structure ensures data persistence, proper separation of concerns, and easy maintenance of the Amiquin Discord bot in containerized environments.
