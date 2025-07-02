# Architecture Overview

This document provides an overview of Amiquin's architecture and design principles.

## High-Level Architecture

Amiquin follows a clean architecture approach with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                       │
│                    (Amiquin.Bot)                           │
├─────────────────────────────────────────────────────────────┤
│                    Business Logic Layer                     │
│                    (Amiquin.Core)                          │
├─────────────────────────────────────────────────────────────┤
│                    Data Access Layer                        │
│                   (Amiquin.Infrastructure)                 │
├─────────────────────────────────────────────────────────────┤
│                    Database Layer                           │
│                   (SQLite/MySQL)                           │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

### Amiquin.Bot (Presentation Layer)

The main bot application responsible for:

- **Discord API Integration**: Handling Discord events and responses
- **Command Processing**: Slash commands and interaction handling
- **User Interface**: Message formatting and user interaction
- **Configuration**: Bot settings and dependency injection

Key Components:
- `Commands/` - Slash command implementations
- `Configurators/` - Dependency injection setup
- `Messages/` - Bot personality and response templates
- `Preconditions/` - Command validation and authorization
- `Console/` - Logging and console output

### Amiquin.Core (Business Logic Layer)

Contains the core business logic and domain models:

- **Domain Models**: Entities and value objects
- **Business Services**: Core functionality implementation
- **Repository Interfaces**: Data access abstractions
- **Utilities**: Helper functions and extensions

Key Components:
- `Models/` - Domain entities (User, Server, Settings, etc.)
- `Services/` - Business logic implementation
- `IRepositories/` - Repository contracts
- `Utilities/` - Helper classes and extensions
- `Abstraction/` - Base classes and interfaces

### Amiquin.Infrastructure (Data Access Layer)

Handles data persistence and external service integration:

- **Repository Implementations**: Data access logic
- **Database Context**: Entity Framework configuration
- **External APIs**: Third-party service integrations
- **Caching**: Performance optimization

Key Components:
- `Repositories/` - Repository implementations
- `AmiquinContext.cs` - Database context
- `Setup.cs` - Infrastructure configuration

### Migrations

Database schema management:

- `Amiquin.Sqlite/` - SQLite migrations
- `Amiquin.MySql/` - MySQL migrations

## Design Principles

### 1. Dependency Inversion

All dependencies flow inward toward the core business logic:

```csharp
// Core defines interfaces
public interface IUserRepository
{
    Task<User> GetUserAsync(ulong discordId);
}

// Infrastructure implements
public class UserRepository : IUserRepository
{
    // Implementation details
}

// Bot layer uses abstraction
public class UserCommands
{
    private readonly IUserRepository _userRepository;
    
    public UserCommands(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }
}
```

### 2. Single Responsibility

Each class has a single, well-defined responsibility:

- Commands handle Discord interactions only
- Services contain business logic
- Repositories manage data access
- Models represent domain concepts

### 3. Open/Closed Principle

The system is designed for extension without modification:

- New commands can be added without changing existing code
- New features extend existing interfaces
- Configuration allows runtime behavior changes

## Key Patterns

### Repository Pattern

Data access is abstracted through repository interfaces:

```csharp
public interface IRepository<T> where T : DbModel
{
    Task<T> GetByIdAsync(int id);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
```

### Service Layer Pattern

Business logic is encapsulated in service classes:

```csharp
public interface IUserService
{
    Task<UserDto> GetUserProfileAsync(ulong discordId);
    Task UpdateUserSettingsAsync(ulong discordId, UserSettings settings);
}
```

### Command Pattern

Discord commands are implemented as separate command classes:

```csharp
[Group("user", "User management commands")]
public class UserCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    [SlashCommand("profile", "View user profile")]
    public async Task ProfileCommand(IUser user = null)
    {
        // Command implementation
    }
}
```

## Data Flow

### Command Execution Flow

1. **Discord Event** → Bot receives slash command
2. **Command Handler** → Routes to appropriate command class
3. **Service Layer** → Executes business logic
4. **Repository Layer** → Persists/retrieves data
5. **Response** → Sends result back to Discord

### Example Flow

```
User types: /user profile @someone
     ↓
Discord.Net receives interaction
     ↓
UserCommands.ProfileCommand() called
     ↓
IUserService.GetUserProfileAsync() called
     ↓
IUserRepository.GetUserAsync() called
     ↓
Database query executed
     ↓
Result formatted and sent to Discord
```

## Database Design

### Core Entities

- **Users**: Discord user information and preferences
- **Servers**: Discord server settings and configuration
- **Commands**: Command usage logging and statistics
- **Messages**: Bot messages and templates

### Relationships

```
Server (1) ←→ (N) Users
Server (1) ←→ (N) Settings
User (1) ←→ (N) CommandLogs
```

## Configuration Management

### Hierarchical Configuration

Configuration is loaded in order of precedence:

1. **Environment Variables** (highest)
2. **appsettings.json**
3. **appsettings.Development.json**
4. **Default values** (lowest)

### Configuration Sections

- **Discord**: Bot token and client configuration
- **Database**: Connection strings and provider settings
- **Logging**: Log levels and output configuration
- **Features**: Feature flags and toggles

## Error Handling

### Centralized Error Handling

- **Command Errors**: Handled by Discord.Net command framework
- **Service Errors**: Wrapped in custom exceptions
- **Database Errors**: Logged and converted to user-friendly messages

### Logging Strategy

- **Structured Logging**: Using Serilog for consistent log format
- **Log Levels**: Appropriate levels for different scenarios
- **Log Sinks**: Console, file, and external logging services

## Performance Considerations

### Caching Strategy

- **In-Memory Caching**: For frequently accessed data
- **Distributed Caching**: For scalability (Redis)
- **Cache Invalidation**: Event-driven cache updates

### Database Optimization

- **Connection Pooling**: Efficient database connections
- **Query Optimization**: Proper indexing and query design
- **Lazy Loading**: Load data only when needed

### Discord API Optimization

- **Rate Limiting**: Respect Discord API limits
- **Bulk Operations**: Batch requests when possible
- **Event Filtering**: Process only relevant events

## Security Considerations

### Data Protection

- **Sensitive Data**: Encrypt tokens and API keys
- **User Privacy**: Minimal data collection
- **Data Retention**: Automatic cleanup of old data

### Access Control

- **Permission System**: Discord role-based permissions
- **Command Preconditions**: Validate user authorization
- **Rate Limiting**: Prevent abuse and spam

## Deployment Architecture

### Container Strategy

- **Multi-Stage Builds**: Optimized Docker images
- **Health Checks**: Container health monitoring
- **Resource Limits**: CPU and memory constraints

### Scalability

- **Horizontal Scaling**: Multiple bot instances
- **Database Scaling**: Read replicas and sharding
- **Load Balancing**: Distribute connections

This architecture ensures maintainability, scalability, and extensibility while providing a solid foundation for Amiquin's features.
