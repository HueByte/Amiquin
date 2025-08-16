# Commands Reference

This document lists all available Amiquin commands and their usage. Commands use Discord's slash command system with modern ComponentsV2 interface.

## Main Commands

### `/chat`

**Description**: Chat with Amiquin using AI conversation  
**Usage**: `/chat <message>`  
**Parameters**:
- `message` (required): Your message to chat with Amiquin

**Examples**:
```
/chat Hello Amiquin! How are you today?
/chat Can you help me with programming?
/chat What's your favorite color?
```

**Features**:
- Contextual responses using conversation history
- Memory integration (when enabled)
- ComponentsV2 interface for rich responses

### `/info`

**Description**: Display bot information and statistics  
**Usage**: `/info`  
**Response**: Bot version, server count, system info, and useful links

### `/sleep`

**Description**: Put Amiquin to sleep for 5 minutes  
**Usage**: `/sleep`  
**Response**: Bot will not respond to commands during sleep period

## Fun Commands

### `/size`

**Description**: Check your... size 📏 (fun command)  
**Usage**: `/size [user]`  
**Parameters**:
- `user` (optional): User to check (defaults to yourself)

### `/color`

**Description**: Display a hex color with visual representation  
**Usage**: `/color <hex>`  
**Parameters**:
- `hex` (required): Hex color code (e.g., #FF5733 or FF5733)

### `/palette`

**Description**: Generate color theory-based palette  
**Usage**: `/palette [harmony] [base_hue]`  
**Parameters**:
- `harmony` (optional): Color harmony type
- `base_hue` (optional): Base hue (0-360 degrees)

### `/avatar`

**Description**: Get a user's avatar  
**Usage**: `/avatar [user]`  
**Parameters**:
- `user` (optional): User to get avatar from (defaults to yourself)

### `/nacho`

**Description**: Give Amiquin a nacho! 🌮  
**Usage**: `/nacho`  
**Response**: Dynamic AI-generated response with nacho count

### `/nacho-leaderboard`

**Description**: View the nacho leaderboard  
**Usage**: `/nacho-leaderboard`  
**Response**: Top nacho givers in the server

## Session Management Commands

### `/session list`

**Description**: View all chat sessions for the server  
**Usage**: `/session list`  
**Response**: Interactive list of all chat sessions with status

### `/session switch`

**Description**: Switch to a different chat session  
**Usage**: `/session switch`  
**Response**: Interactive session selector

### `/session create`

**Description**: Create a new chat session  
**Usage**: `/session create <name>`  
**Parameters**:
- `name` (required): Name for the new session (1-50 characters)

### `/session rename`

**Description**: Rename the current active session  
**Usage**: `/session rename <name>`  
**Parameters**:
- `name` (required): New name for the session (1-50 characters)

### `/session delete`

**Description**: Delete a session (cannot delete the last session)  
**Usage**: `/session delete <name>`  
**Parameters**:
- `name` (required): Name of the session to delete

## Admin Commands

> **Note**: These commands are typically available to administrators and may vary based on server configuration.

### Configuration Commands

Admin configuration commands are available through the admin module for server management. These include:

- Server-wide feature toggles
- Bot behavior configuration
- User permission management
- Channel-specific settings

## Memory Commands

> **Note**: Memory commands are available when the memory system is enabled.

### Memory Management

Memory commands allow users to interact with the AI memory system:

- View stored memories
- Search conversation history
- Manage personal memory data
- Configure memory preferences

These commands are integrated into the main chat system and accessed through user settings.

## NSFW Commands

> **Note**: NSFW commands are only available in age-restricted channels and when enabled by server administrators.

### Adult Content Commands

NSFW commands provide adult content features with safety controls:

- Image generation and fetching
- Content filtering and safety measures
- Server-specific enablement
- Age-restricted channel requirements

These commands follow Discord's Terms of Service and community guidelines.

## Voice/TTS Commands

> **Note**: Voice features require proper voice channel permissions.

### Text-to-Speech Commands

Voice commands provide text-to-speech functionality:

- Join/leave voice channels
- Convert text to speech
- Voice settings and preferences
- Queue management for voice messages

Voice features use Piper TTS engine for high-quality speech synthesis.

## Developer Commands

> **Note**: Developer commands are restricted to bot developers and authorized users.

### Development Tools

Developer commands provide debugging and development functionality:

- System diagnostics
- Performance monitoring
- Configuration testing
- Debug information

These commands are not available to regular users and require special permissions.

## Command Categories and Access

| Command Category | Required Permissions | Notes |
|------------------|---------------------|-------|
| Main Commands | None | Available to all users |
| Fun Commands | None | Safe for all users |
| Session Management | None | Per-user session control |
| Memory Commands | None | When memory system is enabled |
| Voice/TTS | Connect, Speak (Voice) | Voice channel permissions |
| NSFW Commands | None* | Age-restricted channels only |
| Admin Commands | Manage Server or higher | Server administration |
| Developer Commands | Bot Developer | Restricted access |

## ComponentsV2 Interface

Amiquin uses Discord's ComponentsV2 system for enhanced user interaction:

- **Rich Text Displays**: Formatted text with markdown support
- **Interactive Buttons**: Action buttons for commands and navigation
- **Media Galleries**: Image and media display
- **Select Menus**: Dropdown selections for complex choices
- **Persistent Components**: Interactive elements that persist across bot restarts

## Features and Integrations

### AI Integration
- **Multiple AI Providers**: OpenAI, Grok, Gemini support
- **Contextual Conversations**: Memory-enhanced chat experiences
- **Smart Responses**: Dynamic, context-aware AI responses

### Memory System
- **Vector Database**: Qdrant-powered semantic memory
- **Conversation Context**: Long-term conversation memory
- **User Preferences**: Personalized interaction learning

### Modern UI
- **ComponentsV2**: Rich, interactive Discord interface
- **Media Support**: Image generation and processing
- **Responsive Design**: Adaptive layouts for different content

### Safety Features
- **Content Filtering**: Automatic inappropriate content detection
- **Age Restrictions**: NSFW content limited to appropriate channels
- **User Controls**: Individual privacy and preference settings

## Getting Help

### In-Discord Help
- Use `/info` to get basic bot information and links
- Check command responses for contextual guidance
- Use interactive components for guided navigation

### External Resources
- **Documentation**: Comprehensive guides and references
- **GitHub Repository**: Source code and issue tracking
- **Community Support**: Discord community for user help

### Troubleshooting
- Ensure bot has necessary permissions in your channel
- Check that commands are typed correctly with required parameters
- Verify feature availability (some commands require specific configurations)
- For NSFW commands, ensure you're in an age-restricted channel

For technical issues or feature requests, please visit our GitHub repository.
