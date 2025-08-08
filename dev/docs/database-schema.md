# Database Schema

This document describes the database schema and data models used by Amiquin.

## Overview

Amiquin uses Entity Framework Core with support for both SQLite and MySQL databases. The database stores server metadata, user messages, configuration toggles, chat sessions, and bot statistics.

## Core Models

### ServerMeta

Represents a Discord server (guild) with its associated configuration.

```csharp
public class ServerMeta : DbModel<ulong>
{
    public ulong Id { get; set; }              // Discord Server ID
    public string ServerName { get; set; }     // Server display name
    public string Persona { get; set; }        // AI persona configuration
    public DateTime CreatedAt { get; set; }    // When server was added
    public DateTime LastUpdated { get; set; }  // Last configuration update
    public bool IsActive { get; set; }         // Whether bot is active
}
```

**Relationships:**
- Has many `Toggle`s (feature flags)
- Has many `Message`s (cached messages)
- Has many `CommandLog`s (command usage history)
- Has many `NachoPack`s (custom content packs)

### Message

Stores Discord messages for caching and analysis.

```csharp
public class Message : DbModel<ulong>
{
    public ulong Id { get; set; }              // Discord Message ID
    public ulong ChannelId { get; set; }       // Discord Channel ID
    public ulong UserId { get; set; }          // Discord User ID
    public ulong ServerId { get; set; }        // Discord Server ID
    public string Content { get; set; }        // Message content
    public DateTime Timestamp { get; set; }    // When message was sent
    public string UserName { get; set; }       // Username at time of message
}
```

### Toggle

Feature flag system for enabling/disabling bot features per server.

```csharp
public class Toggle : DbModel<int>
{
    public int Id { get; set; }                // Primary key
    public ulong ServerId { get; set; }        // Discord Server ID
    public string Name { get; set; }           // Toggle name (e.g., "EnableTTS")
    public bool IsEnabled { get; set; }        // Whether feature is enabled
    public DateTime CreatedAt { get; set; }    // When toggle was created
    public DateTime? UpdatedAt { get; set; }   // Last update time
}
```

**Available Toggles:**
- `EnableTTS` - Text-to-speech functionality
- `EnableJoinMessage` - Welcome messages for new members
- `EnableChat` - AI chat functionality
- `EnableNews` - News API integration (system-exclusive)

## Chat Session Models

### ChatSession

Tracks AI chat sessions per user and channel.

```csharp
public class ChatSession
{
    public int Id { get; set; }                     // Primary key
    public string SessionId { get; set; }           // Unique session identifier
    public string UserId { get; set; }              // Discord User ID
    public string ChannelId { get; set; }           // Discord Channel ID
    public string ServerId { get; set; }            // Discord Server ID
    public DateTime CreatedAt { get; set; }         // Session creation time
    public DateTime LastActivityAt { get; set; }    // Last message time
    public int MessageCount { get; set; }           // Number of messages
    public int EstimatedTokens { get; set; }        // Token usage estimate
    public string Model { get; set; }               // AI model used
    public bool IsActive { get; set; }              // Session status
    public string? Metadata { get; set; }           // JSON metadata
}
```

**Session ID Format:** `{ServerId}_{ChannelId}_{UserId}`

### SessionMessage

Individual messages within a chat session.

```csharp
public class SessionMessage
{
    public int Id { get; set; }                     // Primary key
    public int ChatSessionId { get; set; }          // Foreign key to ChatSession
    public string? DiscordMessageId { get; set; }   // Discord Message ID
    public string Role { get; set; }                // Message role (user/assistant/system)
    public string Content { get; set; }             // Message content
    public DateTime CreatedAt { get; set; }         // Message timestamp
    public int EstimatedTokens { get; set; }        // Token estimate
    public bool IncludeInContext { get; set; }      // Whether to include in AI context
    public string? Metadata { get; set; }           // JSON metadata
}
```

**Message Roles:**
- `user` - User input messages
- `assistant` - AI responses
- `system` - System/persona messages

## Supporting Models

### CommandLog

Tracks command usage for analytics and debugging.

```csharp
public class CommandLog : DbModel<int>
{
    public int Id { get; set; }                 // Primary key
    public ulong ServerId { get; set; }         // Discord Server ID
    public ulong UserId { get; set; }           // Discord User ID
    public ulong ChannelId { get; set; }        // Discord Channel ID
    public string CommandName { get; set; }     // Command that was executed
    public string? Parameters { get; set; }     // Command parameters
    public bool IsSuccessful { get; set; }      // Whether command succeeded
    public string? ErrorMessage { get; set; }   // Error message if failed
    public DateTime ExecutedAt { get; set; }    // Execution timestamp
    public TimeSpan ExecutionTime { get; set; } // How long command took
}
```

### BotStatistics

Global bot usage statistics.

```csharp
public class BotStatistics : DbModel<int>
{
    public int Id { get; set; }                     // Primary key
    public string MetricName { get; set; }          // Metric identifier
    public double Value { get; set; }               // Metric value
    public DateTime Timestamp { get; set; }         // When metric was recorded
    public string? Context { get; set; }            // Additional context
}
```

### NachoPack

Custom content packs for servers (extensibility feature).

```csharp
public class NachoPack : DbModel<int>
{
    public int Id { get; set; }                 // Primary key
    public ulong ServerId { get; set; }         // Discord Server ID
    public string Name { get; set; }            // Pack name
    public string Description { get; set; }     // Pack description
    public string Content { get; set; }         // Pack content (JSON)
    public bool IsActive { get; set; }          // Whether pack is enabled
    public DateTime CreatedAt { get; set; }     // Creation timestamp
    public DateTime? UpdatedAt { get; set; }    // Last update time
}
```

## Database Configuration

### Connection Strings

**SQLite (Default):**
```json
{
  "ConnectionStrings": {
    "AmiquinContext": "Data Source=Data/Database/amiquin.db"
  }
}
```

**MySQL:**
```json
{
  "ConnectionStrings": {
    "AmiquinContext": "Server=localhost;Database=amiquin;Uid=username;Pwd=password;"
  }
}
```

### Indexes

Performance indexes are automatically created for:

- `ChatSession.SessionId` (unique)
- `ChatSession` composite index on `(UserId, ChannelId, ServerId)`
- `SessionMessage.DiscordMessageId`
- `SessionMessage.CreatedAt`
- Foreign key relationships

### Migrations

Database migrations are stored in:
- `Migrations/Amiquin.Sqlite/` - SQLite migrations
- `Migrations/Amiquin.MySql/` - MySQL migrations

**Generate New Migration:**
```bash
./scripts/generate-migrations.sh MigrationName
```

## Entity Relationships

```
ServerMeta (1) ←→ (N) Toggle
ServerMeta (1) ←→ (N) Message  
ServerMeta (1) ←→ (N) CommandLog
ServerMeta (1) ←→ (N) NachoPack
ServerMeta (1) ←→ (N) ChatSession

ChatSession (1) ←→ (N) SessionMessage
```

## Data Access

All database access goes through the repository pattern:

- `IServerMetaRepository` - Server metadata operations
- `IMessageRepository` - Message caching operations  
- `IToggleRepository` - Feature toggle management
- `ICommandLogRepository` - Command logging
- `INachoRepository` - Custom content management
- `IBotStatisticsRepository` - Analytics data

Repositories are automatically registered via dependency injection and provide standard CRUD operations plus custom query methods.

---

*Last updated: January 2025*