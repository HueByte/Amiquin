# Getting Started with Amiquin

Welcome to **Amiquin**, your intelligent Discord companion! This guide will help you understand what Amiquin is, what it can do, and how to get it running in your Discord server.

## What is Amiquin?

Amiquin is a sophisticated Discord bot designed to be your **virtual clanmate**. Unlike traditional bots that simply respond to commands, Amiquin uses advanced AI to:

- 🧠 **Remember conversations** and build context over time
- 💬 **Engage naturally** with server members using AI conversation
- 🎮 **Provide entertainment** through games and interactive features
- 🛠️ **Assist with tasks** using utility commands and server management
- 🎨 **Generate content** including images, text, and multimedia responses

## Key Features at a Glance

### 🤖 AI-Powered Conversations
- **Natural Language Processing** - Powered by OpenAI GPT models
- **Contextual Memory** - Remembers past conversations using vector database technology
- **Personality Adaptation** - Adjusts behavior based on server culture and preferences
- **Multi-Provider Support** - Works with OpenAI, Gemini, and other AI providers

### 💬 Discord Integration
- **Slash Commands** - Modern Discord interface with autocomplete and validation
- **Interactive Components** - Buttons, menus, and modals for rich interactions
- **Voice Support** - Text-to-speech and voice channel integration
- **Per-Server Configuration** - Customizable settings for each Discord server

### 🎯 Smart Features
- **Session Management** - Tracks conversations per user and channel
- **Content Safety** - Built-in moderation and content filtering
- **Background Jobs** - Automated tasks and maintenance
- **Health Monitoring** - Real-time status and performance metrics

## Quick Start Options

Choose your preferred deployment method:

### Option 1: Hosted Instance (Recommended)
If Hue is hosting a public instance:
1. [Invite Amiquin](invite-link) to your Discord server
2. Follow the [Configuration Guide](configuration.md) to set up features
3. Start chatting with `/chat` command

### Option 2: Self-Hosting with Docker (Easy)
For your own private instance:
1. Follow the [Docker Setup Guide](docker-setup.md)
2. Configure your environment variables
3. Deploy with `docker-compose up`

### Option 3: Local Development (Advanced)
For developers and contributors:
1. Follow the [Development Setup](development.md)
2. Build and run locally with .NET 9.0
3. Contribute to the project via GitHub

## First Steps After Installation

### 1. Basic Configuration
```
/configure setup
```
This command opens the interactive configuration panel where you can:
- Set up AI providers (OpenAI API key required)
- Configure server-specific settings
- Enable/disable features per channel or user

### 2. Test Basic Functionality
```
/chat message: Hello Amiquin!
```
Start your first conversation and see the AI respond naturally.

### 3. Explore Commands
```
/help
```
View all available commands organized by category.

### 4. Set Up Advanced Features

#### Enable Memory System
```
/configure memory enable
```
This allows Amiquin to remember conversations and build context over time.

#### Configure Voice Features
```
/configure voice setup
```
Set up text-to-speech and voice channel integration.

#### Set Content Preferences
```
/configure safety
```
Configure content filtering and safety settings for your server.

## Understanding Amiquin's Personality

Amiquin is designed to be:

- **Friendly and Approachable** - Uses casual, gaming-oriented language
- **Helpful and Knowledgeable** - Provides assistance while maintaining conversation
- **Adaptive** - Learns your server's culture and communication style
- **Safety-Conscious** - Maintains appropriate boundaries and content standards

## Essential Commands to Know

### Conversation Commands
- `/chat` - Start or continue an AI conversation
- `/persona` - View or modify Amiquin's personality settings
- `/session` - Manage conversation sessions and history

### Utility Commands
- `/help` - Get help with commands and features
- `/status` - Check bot status and performance
- `/configure` - Access all configuration options

### Fun Commands
- `/fun` - Access entertainment features and games
- `/image` - Generate or manipulate images
- `/voice` - Text-to-speech features

### Admin Commands (Server Administrators)
- `/admin` - Server management and moderation tools
- `/logs` - View command usage and audit logs
- `/toggle` - Enable/disable features server-wide

## Understanding Sessions and Memory

### Conversation Sessions
Amiquin tracks conversations in **sessions** that are:
- **Per-User**: Each user has their own conversation history
- **Per-Channel**: Different channels maintain separate contexts
- **Persistent**: Sessions continue across bot restarts
- **Manageable**: Users can clear or reset their sessions

### AI Memory System
When enabled, Amiquin uses a sophisticated memory system:
- **Vector Database**: Stores conversation context as searchable vectors
- **Semantic Search**: Finds relevant past conversations automatically
- **Smart Summarization**: Condenses long conversations into key points
- **Privacy Controls**: Users can manage their own memory data

## Configuration Overview

Amiquin offers extensive configuration options:

### Server-Level Settings
- Feature enablement (AI chat, voice, NSFW content)
- Default personality and behavior
- Content filtering levels
- Command permissions

### User-Level Settings
- Personal conversation preferences
- Memory and privacy settings
- Individual feature toggles
- Notification preferences

### Channel-Level Settings
- Per-channel feature availability
- Topic-specific personalities
- Response behavior modification

## Getting Help

### In-Discord Help
- `/help` - Interactive help system
- `/support` - Get support information
- `/docs` - Link to this documentation

### Community Resources
- **Documentation**: Comprehensive guides and references
- **GitHub Issues**: Bug reports and feature requests
- **Developer Community**: Contribute to the project

### Troubleshooting Common Issues

#### "Bot not responding"
1. Check bot permissions in channel
2. Verify bot is online with `/status`
3. Ensure commands are typed correctly

#### "AI features not working"
1. Verify OpenAI API key is configured
2. Check `/configure ai` settings
3. Ensure memory system is enabled if needed

#### "Voice features unavailable"
1. Check bot has voice permissions
2. Verify Piper TTS is configured
3. Ensure ffmpeg dependencies are available

## Next Steps

Now that you understand the basics:

1. **[Complete Setup](installation.md)** - Full installation and configuration
2. **[Learn All Commands](commands.md)** - Comprehensive command reference
3. **[Explore Features](user-guide.md)** - Detailed feature tutorials
4. **[Configure Advanced Features](configuration.md)** - Customize Amiquin for your server

## Best Practices

### For Server Administrators
- Start with default settings and adjust based on community feedback
- Set clear guidelines for AI interaction in your server
- Monitor usage through logs and adjust permissions as needed
- Regularly review and update configuration as your server grows

### For Users
- Be patient as Amiquin learns your server's communication style
- Use descriptive language to help the AI understand context
- Respect other users' privacy and conversation boundaries
- Report any issues or inappropriate responses to server administrators

---

**Ready to begin?** Start with the [Installation Guide](installation.md) to get Amiquin running in your server!