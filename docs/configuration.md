# Configuration Guide

This guide covers how to configure Amiquin for your Discord server and customize its behavior.

## Bot Configuration

### Environment Variables

Amiquin uses environment variables for configuration. Create a `.env` file or set these in your hosting environment:

```env
# Required
DISCORD_TOKEN=your_bot_token_here

# Database (choose one)
DATABASE_TYPE=sqlite
DATABASE_CONNECTION=Data Source=amiquin.db

# OR for MySQL
# DATABASE_TYPE=mysql
# DATABASE_CONNECTION=Server=localhost;Database=amiquin;User=amiquin;Password=your_password;

# Optional
LOG_LEVEL=Information
ENVIRONMENT=Production
PREFIX=!
```

### Configuration Files

#### appsettings.json

```json
{
  "Discord": {
    "Token": "your_token_here",
    "Prefix": "!",
    "ActivityType": "Playing",
    "ActivityName": "with Discord.Net"
  },
  "Database": {
    "Type": "sqlite",
    "ConnectionString": "Data Source=amiquin.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  }
}
```

## Server Settings

### Basic Setup

After inviting Amiquin to your server, configure basic settings:

```sh
/config welcome_channel #general
/config log_channel #bot-logs
/config prefix !
```

### Available Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `welcome_channel` | Channel for welcome messages | None |
| `log_channel` | Channel for bot logs | None |
| `prefix` | Command prefix (legacy) | `!` |
| `auto_role` | Automatic role for new members | None |
| `modlog_channel` | Channel for moderation logs | None |

### Permission Setup

#### Required Bot Permissions

- **Send Messages** - Basic functionality
- **Use Slash Commands** - Modern command interface
- **Embed Links** - Rich message formatting
- **Read Message History** - Context awareness

#### Optional Permissions

- **Manage Messages** - Moderation features
- **Kick Members** - Kick command
- **Ban Members** - Ban/unban commands
- **Manage Roles** - Role management
- **Connect** - Voice features
- **Speak** - Voice features

### Role Configuration

#### Setting Up Moderation Roles

1. Create moderation roles in your server
2. Assign appropriate permissions to roles
3. Configure bot to recognize these roles:

```sh
/config mod_role @Moderator
/config admin_role @Administrator
```

## Feature Configuration

### Welcome Messages

Configure automatic welcome messages for new members:

```sh
/config welcome_channel #general
/config welcome_message "Welcome {user} to {server}! Please read #rules"
```

**Available Variables:**

- `{user}` - User mention
- `{username}` - Username
- `{server}` - Server name
- `{membercount}` - Current member count

### Auto Moderation

Enable automatic moderation features:

```sh
/config automod_enabled true
/config automod_spam_detection true
/config automod_link_filter true
/config automod_bad_words true
```

### Logging

Configure what events to log:

```sh
/config log_joins true
/config log_leaves true
/config log_bans true
/config log_kicks true
/config log_message_edits true
/config log_message_deletes true
```

## Advanced Configuration

### Database Configuration

#### SQLite (Default)

SQLite requires no additional setup:

```env
DATABASE_TYPE=sqlite
DATABASE_CONNECTION=Data Source=amiquin.db
```

#### MySQL

For MySQL, set up the database first:

```sql
CREATE DATABASE amiquin;
CREATE USER 'amiquin'@'%' IDENTIFIED BY 'your_password';
GRANT ALL PRIVILEGES ON amiquin.* TO 'amiquin'@'%';
FLUSH PRIVILEGES;
```

Then configure:

```env
DATABASE_TYPE=mysql
DATABASE_CONNECTION=Server=localhost;Database=amiquin;User=amiquin;Password=your_password;
```

### Custom Commands

Create custom commands for your server:

```sh
/custom add greet "Hello {user}, welcome to our awesome server!"
/custom add rules "Please read our rules in #rules channel"
```

### Scheduled Tasks

Configure recurring tasks:

```sh
/schedule add daily "Good morning everyone!" #general 09:00
/schedule add weekly "Weekly server update!" #announcements monday 18:00
```

## Troubleshooting

### Common Issues

#### Bot Not Responding

**Symptoms**: Bot appears online but doesn't respond to commands

**Solutions**:

1. Check bot permissions in the channel
2. Verify bot has "Use Slash Commands" permission
3. Check if bot is rate-limited
4. Restart the bot

#### Permission Errors

**Symptoms**: "Missing permissions" errors

**Solutions**:

1. Check bot role hierarchy (bot role should be above managed roles)
2. Verify channel-specific permissions
3. Grant necessary permissions in server settings

#### Database Errors

**Symptoms**: Commands fail with database errors

**Solutions**:

1. Check database connection string
2. Verify database permissions
3. Check disk space (for SQLite)
4. Restart bot to reset connections

### Getting Help

1. **Check bot logs** in your configured log channel
2. **Use the help command**: `/help`
3. **Join support server**: [Discord Server Link]
4. **Create GitHub issue**: [GitHub Issues](https://github.com/huebyte/Amiquin/issues)

### Debug Mode

Enable debug mode for detailed logging:

```env
LOG_LEVEL=Debug
```

**Warning**: Debug mode generates lots of logs. Only use for troubleshooting.

## Migration

### From Other Bots

When migrating from other Discord bots:

1. **Export settings** from your previous bot (if possible)
2. **Configure Amiquin** with similar settings
3. **Test functionality** before removing the old bot
4. **Update documentation** and inform users

### Backup and Restore

#### Backing Up Configuration

```bash
# Backup SQLite database
cp amiquin.db amiquin.db.backup

# Backup configuration
cp appsettings.json appsettings.json.backup
```

#### Restoring Configuration

```bash
# Restore database
cp amiquin.db.backup amiquin.db

# Restore configuration
cp appsettings.json.backup appsettings.json
```

## Performance Optimization

### Large Servers

For servers with many members:

1. **Enable database indexing**
2. **Use MySQL instead of SQLite**
3. **Configure caching**:

   ```env
   CACHE_ENABLED=true
   CACHE_EXPIRY=3600
   ```

### Resource Limits

Monitor and configure resource usage:

```env
MAX_MEMORY_MB=512
MAX_CPU_PERCENT=80
RATE_LIMIT_PER_USER=5
RATE_LIMIT_WINDOW=60
```

This completes the basic configuration guide. For more advanced configurations, check the [Development Guide](development.md) or consult the API documentation.
