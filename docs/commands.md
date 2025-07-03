# Commands Reference

This document lists all available Amiquin commands and their usage.

## General Commands

### `/help`

**Description**: Display help information and available commands  
**Usage**: `/help [command]`  
**Parameters**:

- `command` (optional): Get help for a specific command

**Examples**:

```cs
/help
/help ping
```

### `/ping`

**Description**: Check bot responsiveness and latency  
**Usage**: `/ping`  
**Response**: Shows bot latency and API response time

### `/info`

**Description**: Display bot information and statistics  
**Usage**: `/info`  
**Response**: Bot version, uptime, server count, and system info

### `/about`

**Description**: Learn about Amiquin and its features  
**Usage**: `/about`  
**Response**: Bot description, links, and credits

## User Commands

### `/user profile`

**Description**: View user profile and statistics  
**Usage**: `/user profile [user]`  
**Parameters**:

- `user` (optional): User to view profile for (defaults to yourself)

### `/user settings`

**Description**: Manage your personal bot settings  
**Usage**: `/user settings`  
**Response**: Interactive settings menu

## Server Commands

> Note: These commands require appropriate permissions

### `/config`

**Description**: Server configuration management  
**Usage**: `/config <setting> <value>`  

#### Available Settings:

- `prefix`: Set command prefix (deprecated in favor of slash commands)
- `welcome_channel`: Set welcome message channel
- `log_channel`: Set bot logging channel
- `auto_role`: Set automatic role for new members

**Examples**:

```sh
/config welcome_channel #general
/config log_channel #bot-logs
/config auto_role @Member
```

## Moderation Commands

> Note: Requires appropriate moderation permissions*

### `/kick`

**Description**: Kick a user from the server  
**Usage**: `/kick <user> [reason]`  
**Parameters**:

- `user`: User to kick
- `reason` (optional): Reason for the kick

### `/ban`

**Description**: Ban a user from the server  
**Usage**: `/ban <user> [reason] [days]`  
**Parameters**:

- `user`: User to ban
- `reason` (optional): Reason for the ban
- `days` (optional): Days of messages to delete (0-7)

### `/unban`

**Description**: Unban a user from the server  
**Usage**: `/unban <user_id> [reason]`  
**Parameters**:

- `user_id`: ID of the user to unban
- `reason` (optional): Reason for the unban

### `/timeout`

**Description**: Timeout a user (mute them temporarily)  
**Usage**: `/timeout <user> <duration> [reason]`  
**Parameters**:

- `user`: User to timeout
- `duration`: Duration (e.g., "10m", "1h", "1d")
- `reason` (optional): Reason for the timeout

### `/warn`

**Description**: Issue a warning to a user  
**Usage**: `/warn <user> <reason>`  
**Parameters**:

- `user`: User to warn
- `reason`: Reason for the warning

## Utility Commands

### `/remind`

**Description**: Set a reminder  
**Usage**: `/remind <time> <message>`  
**Parameters**:

- `time`: When to remind (e.g., "in 30m", "tomorrow at 9am")
- `message`: Reminder message

### `/poll`

**Description**: Create a poll  
**Usage**: `/poll <question> <option1> <option2> [option3] [option4]`  
**Parameters**:

- `question`: Poll question
- `option1-4`: Poll options (2-4 options supported)

### `/weather`

**Description**: Get weather information  
**Usage**: `/weather <location>`  
**Parameters**:

- `location`: City name or location

## Fun Commands

### `/8ball`

**Description**: Ask the magic 8-ball a question  
**Usage**: `/8ball <question>`  
**Parameters**:

- `question`: Your yes/no question

### `/dice`

**Description**: Roll dice  
**Usage**: `/dice [sides] [count]`  
**Parameters**:

- `sides` (optional): Number of sides (default: 6)
- `count` (optional): Number of dice (default: 1)

### `/flip`

**Description**: Flip a coin  
**Usage**: `/flip`  
**Response**: Heads or tails

### `/random`

**Description**: Generate random numbers or pick from options  
**Usage**: `/random <min> <max>` or `/random <option1,option2,option3>`  
**Parameters**:

- `min`: Minimum number
- `max`: Maximum number
- OR comma-separated list of options

## Music Commands

> Note: Bot must be in a voice channel*

### `/play`

**Description**: Play music from various sources  
**Usage**: `/play <query>`  
**Parameters**:

- `query`: YouTube URL, search term, or Spotify link

### `/pause`

**Description**: Pause current playback  
**Usage**: `/pause`

### `/resume`

**Description**: Resume paused playback  
**Usage**: `/resume`

### `/stop`

**Description**: Stop playback and clear queue  
**Usage**: `/stop`

### `/skip`

**Description**: Skip current track  
**Usage**: `/skip`

### `/queue`

**Description**: View current music queue  
**Usage**: `/queue`

### `/volume`

**Description**: Adjust playback volume  
**Usage**: `/volume <level>`  
**Parameters**:

- `level`: Volume level (0-100)

## Admin Commands

> Note: These commands require administrator permissions*

### `/admin purge`

**Description**: Delete multiple messages  
**Usage**: `/admin purge <count> [user]`  
**Parameters**:

- `count`: Number of messages to delete (1-100)
- `user` (optional): Only delete messages from this user

### `/admin reload`

**Description**: Reload bot configuration  
**Usage**: `/admin reload [module]`  
**Parameters**:

- `module` (optional): Specific module to reload

### `/admin stats`

**Description**: View detailed bot statistics  
**Usage**: `/admin stats`  
**Response**: Comprehensive bot statistics and performance metrics

## Permission Requirements

| Command Category | Required Permissions |
|------------------|---------------------|
| General | None |
| User | None |
| Server Config | Manage Server |
| Moderation | Various moderation permissions |
| Utility | None |
| Fun | None |
| Music | Connect, Speak (Voice) |
| Admin | Administrator |

## Command Aliases

Some commands have shorter aliases:

| Full Command | Alias |
|-------------|--------|
| `/user profile` | `/profile` |
| `/admin stats` | `/stats` |
| `/8ball` | `/8b` |

## Error Messages

Common error messages and their meanings:

- **"Missing permissions"**: You don't have the required permissions
- **"Bot missing permissions"**: The bot lacks necessary permissions
- **"User not found"**: The specified user doesn't exist or isn't in the server
- **"Invalid duration"**: Time format is incorrect (use formats like "1h", "30m")
- **"Command on cooldown"**: Command is rate-limited, try again later

## Getting Help

If you need help with a specific command:

1. Use `/help <command>` for detailed information
2. Check this documentation
3. Ask in our support server
4. Create an issue on GitHub

For command suggestions or feature requests, please use our GitHub repository or join our Discord server.
