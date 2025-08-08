# Project Structure

This document describes the architectural organization of the Amiquin codebase.

## Solution Structure

```
Amiquin/
├── source/
│   ├── Amiquin.Bot/              # Discord bot application layer
│   ├── Amiquin.Core/             # Business logic and services
│   ├── Amiquin.Infrastructure/   # Data access and external services
│   ├── Amiquin.Tests/           # Unit tests
│   └── Amiquin.IntegrationTests/ # Integration tests
├── dev/                         # Documentation and development tools
├── Data/                        # Runtime data storage
├── scripts/                     # Build and utility scripts
└── .env                         # Environment configuration
```

## Architecture Layers

### Presentation Layer - Amiquin.Bot
- Discord command handlers
- User interaction logic
- Bot initialization and hosting
- Configuration management

### Business Logic Layer - Amiquin.Core
- Domain models and entities
- Service interfaces and implementations
- Chat and AI integration
- Session management
- Business rules and validation

### Data Access Layer - Amiquin.Infrastructure
- Entity Framework contexts
- Repository implementations
- External API integrations
- Database migrations

## Key Components

### Chat System
- **Session Management**: Per-user, per-channel conversation tracking
- **Message History**: Conversation context with token optimization
- **AI Integration**: OpenAI API integration with multiple models

### Discord Integration
- **Commands**: Slash command implementations
- **Events**: Discord event handling
- **Voice**: TTS and voice channel support

### Configuration
- **Options Pattern**: Strongly-typed configuration
- **Environment Variables**: Runtime configuration overrides
- **Validation**: Startup configuration validation