# Amiquin Documentation

Welcome to the comprehensive documentation for **Amiquin**, a modern Discord bot built with .NET 9.0 and powered by advanced AI capabilities. Amiquin serves as your virtual clanmate, providing intelligent conversation, entertainment, and utility features for Discord communities.

## 🚀 Quick Start

New to Amiquin? Get up and running quickly:

1. **[Getting Started](getting-started.md)** - Overview and first steps
2. **[Installation](installation.md)** - Deploy Amiquin to your server
3. **[Configuration](configuration.md)** - Set up features and AI providers
4. **[User Guide](user-guide.md)** - Learn all the features

## 📖 User Documentation

Everything users need to know:

### Setup & Configuration
- **[Getting Started](getting-started.md)** - First steps with Amiquin
- **[Installation](installation.md)** - Complete installation guide
- **[Configuration](configuration.md)** - Bot setup and feature configuration
- **[Docker Setup](docker-setup.md)** - Container deployment guide
- **[Production Deployment](production-deployment.md)** - Enterprise deployment guide

### Usage & Features
- **[Commands Reference](commands.md)** - Complete command documentation
- **[User Guide](user-guide.md)** - Feature tutorials and usage examples
- **[Memory System](memory-system.md)** - AI memory and conversation context

## 🛠️ Developer Documentation

For contributors and developers:

### Architecture & Design
- **[Project Structure](project-structure.md)** - Codebase organization
- **[Architecture](architecture.md)** - System design overview
- **[Chat Service Architecture](chat-service-architecture.md)** - AI conversation system
- **[Database Schema](database-schema.md)** - Data models and relationships

### Development Workflow
- **[Development](development.md)** - Local development setup
- **[Git Setup](git-setup.md)** - Version control workflow
- **[Contributing](contributing.md)** - Contribution guidelines
- **[Scripts Reference](scripts-reference.md)** - Build and deployment scripts

### Technical Reference
- **[Database Providers](database-provider-connections.md)** - Database configuration
- **[API Documentation](../api/)** - Generated code documentation
- **[Changelog](changelog/)** - Version history and release notes

## ✨ Key Features

Amiquin is designed as your virtual clanmate with these capabilities:

### 🧠 AI-Powered Intelligence
- **Advanced Conversation** - OpenAI GPT integration with contextual awareness
- **Memory System** - Qdrant vector database for long-term conversation memory
- **Multiple AI Providers** - Support for OpenAI, Gemini, and other LLM providers
- **Smart Context Management** - Automatic conversation history optimization

### 💬 Discord Integration
- **Slash Commands** - Modern Discord interaction system
- **Interactive Components** - Buttons, select menus, and modals
- **Session Management** - Per-user, per-channel conversation tracking
- **Server Configuration** - Customizable settings per Discord server

### 🎵 Multimedia Support
- **Voice Integration** - TTS and voice channel support using Piper
- **Image Processing** - SixLabors.ImageSharp for advanced image manipulation
- **NSFW Content** - Optional adult content features with safety controls

### 🏗️ Modern Architecture
- **Clean Architecture** - Separation of concerns with Core/Infrastructure/Presentation layers
- **Vector Database** - Qdrant for semantic search and memory persistence
- **Entity Framework Core** - Support for SQLite, MySQL, and PostgreSQL
- **Background Jobs** - Scheduled tasks and maintenance operations

### 🚀 Deployment & Operations
- **Docker Support** - Full containerization with Docker Compose
- **Production Ready** - Enterprise deployment configurations
- **Health Monitoring** - Built-in health checks and logging
- **Scalable Design** - Horizontal and vertical scaling support

## 🎯 Project Philosophy

Amiquin is built around the concept of a **virtual clanmate** - an AI companion that:

- **Learns and Remembers** - Uses vector memory to build relationships over time
- **Adapts to Communities** - Configurable personality and behavior per server
- **Provides Entertainment** - Games, fun commands, and interactive features
- **Assists with Tasks** - Utility commands and server management features
- **Maintains Safety** - Robust content filtering and moderation tools

## 🛡️ Safety & Moderation

Amiquin includes comprehensive safety features:

- **Content Filtering** - Automatic detection and handling of inappropriate content
- **User Controls** - Individual user settings and privacy options
- **Server Configuration** - Admin controls for feature enablement
- **Audit Logging** - Comprehensive command and interaction logging

## 📊 Performance & Scalability

Built for performance and growth:

- **Efficient Memory Usage** - Smart caching and memory management
- **Database Optimization** - Indexed queries and connection pooling
- **Vector Search** - Fast semantic similarity using HNSW indexing
- **Resource Monitoring** - Built-in metrics and health endpoints

## 🌐 Community & Support

Join the Amiquin community:

- **[GitHub Repository](https://github.com/huebyte/amiquin)** - Source code and development
- **[Issues & Features](https://github.com/huebyte/amiquin/issues)** - Bug reports and feature requests
- **[Contributing Guide](contributing.md)** - Help improve Amiquin
- **[Development Setup](development.md)** - Contributor environment setup

## 📈 Version Information

- **Current Version**: v1.0.0 "Genesis"
- **Framework**: .NET 9.0
- **Discord.Net**: 3.18.0
- **Database**: Entity Framework Core 9.0
- **Vector DB**: Qdrant latest
- **AI Provider**: OpenAI GPT-4 series

---

*Amiquin - Your Virtual Clanmate | Documentation last updated: August 2025*