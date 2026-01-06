# ☁️ Amiquin ☁️

<p align="center">
    <img src="./Assets/bannah.gif" alt="Amiquin" width="100%"/>
</p>

Amiquin is a modular and extensible application designed to streamline development with a focus on configurability, logging, and dependency injection. This project leverages modern .NET technologies to provide a solid foundation for building applications.
The goal is to create a robust, fun and scalable bot.

## 📚 Documentation

For comprehensive documentation, guides, and API references, visit our [GitHub Pages documentation](https://huebyte.github.io/Amiquin/).

The documentation includes:

- **Getting Started Guide** - Step-by-step setup and configuration
- **Commands Reference** - Complete list of available Discord commands
- **Architecture Overview** - Technical details about the project structure
- **Development Guide** - Contributing and development best practices
- **Configuration Guide** - Detailed configuration options and examples

## ⚗️ Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (version 9.0 or later)
- [Docker](https://www.docker.com/) (optional)
- [ffmpeg](https://ffmpeg.org/download.html)
- [Piper](https://github.com/rhasspy/piper)

## ✨ Installation

### � Quick Start (Recommended)

The easiest way to get started is using the automated setup scripts:

**Windows (PowerShell):**

```powershell
# Run the interactive setup script
./scripts/setup-project.ps1

# Or use default values for quick setup
./scripts/setup-project.ps1 -Default

# Production setup with enhanced security
./scripts/setup-project.ps1 -Production
```

**Linux/macOS (Bash):**

```bash
# Run the interactive setup script
./scripts/setup-project.sh

# Or use default values for quick setup
./scripts/setup-project.sh --default

# Non-interactive mode
./scripts/setup-project.sh --non-interactive
```

The setup script will:

- ✓ Create `.env` and `appsettings.json` configuration files
- ✓ Prompt for required API keys (Discord, OpenAI)
- ✓ Configure optional features (memory system, web search, voice)
- ✓ Set up database (SQLite or MySQL)
- ✓ Create necessary data directories
- ✓ Build the solution

### �🚢 Docker

Docker is recommended for running the application in a containerized environment. (Docker required)

1. Clone the repository:

```bash
git clone https://github.com/your-repo/amiquin.git
cd amiquin
```

1. Configure the application:
   - Copy the `.env.example` file to `.env`. Update the values as needed.
   - Copy the `appsettings.example.json` file to `appsettings.json`. Update the values as needed.

2. Run docker:

```bash
docker-compose up
```

### 👨‍💻 Local

If you want to run the application locally, follow the steps below. (You have to install the prerequisites)

> Install the prerequisites before running the application.
> Piper for the Text to Speech (TTS) feature.
> ffmpeg for the audio streaming to voicechat feature.

1. Clone the repository:

```bash
git clone https://github.com/your-repo/amiquin.git
cd amiquin
```

1. Restore dependencies:

```bash
dotnet restore
```

1. Configure the application:
   - Copy the `.env.example` file to `.env`. Update the values as needed.
   - Copy the `appsettings.example.json` file to `appsettings.json`. Update the values as needed.

2. Build the application:

```bash
dotnet run --project source/Amiquin.Bot -c Release
```

or if you want to create a self-contained executable:\
[Publish Documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish)

```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

## 🚀 Deployment

### Step-by-Step Deployment Guide

Follow these steps to deploy Amiquin to production:

#### Step 1: Prerequisites

Before starting, ensure you have:

- ✅ **Discord Bot Token** - Create a bot at [Discord Developer Portal](https://discord.com/developers/applications)
- ✅ **OpenAI API Key** - Get your key from [OpenAI Platform](https://platform.openai.com/api-keys)
- ✅ **Docker & Docker Compose** - [Install Docker](https://docs.docker.com/get-docker/)
- ✅ **Git** - For cloning the repository

#### Step 2: Clone and Navigate

```bash
# Clone the repository
git clone https://github.com/HueByte/Amiquin.git
cd Amiquin
```

#### Step 3: Run Setup Script

The setup script will configure everything for you interactively.

**Windows (PowerShell):**

```powershell
# Interactive setup with prompts
./scripts/setup-project.ps1

# Or for production with security hardening
./scripts/setup-project.ps1 -Production
```

**Linux/macOS (Bash):**

```bash
# Make scripts executable (first time only)
chmod +x scripts/*.sh

# Interactive setup with prompts
./scripts/setup-project.sh
```

**What the script does:**

1. Prompts for Discord bot token
2. Prompts for OpenAI API key (or other LLM provider)
3. Asks about optional features:
   - Web search (DuckDuckGo/Google/Bing)
   - Memory system (Qdrant)
   - Voice/TTS features
4. Selects database (SQLite for dev, MySQL for production)
5. Generates secure passwords automatically
6. Creates `.env` and `appsettings.json` files
7. Builds the solution

#### Step 4: Validate Configuration

Run the pre-deployment checklist to ensure everything is configured correctly:

**Windows:**

```powershell
./scripts/pre-deployment-checklist.ps1 -Production
```

**Linux/macOS:**

```bash
./scripts/pre-deployment-checklist.sh --production
```

**The checklist validates:**

- ✓ Configuration files exist and are complete
- ✓ API keys are set
- ✓ Dependencies are installed
- ✓ Project builds successfully
- ✓ Database configuration is correct
- ✓ Security settings are appropriate for production

**Fix any errors** before proceeding. The script will tell you exactly what's missing.

#### Step 5: Deploy with Docker Compose

Choose your deployment profile based on your needs:

> **⚠️ Important:** When using Docker Compose, ensure your `.env` file uses Docker service names (`mysql`, `qdrant`) instead of `localhost`. The setup script handles this automatically.

**5a. Development Deployment (SQLite + Qdrant):**

```bash
# Start all development services
docker-compose --profile dev up -d

# View logs (note: service name is 'amiquinbot')
docker-compose logs -f amiquinbot
```

**5b. Production Deployment (MySQL + Qdrant):**

```bash
# Start production services with MySQL
docker-compose --profile prod up -d

# View logs
docker-compose logs -f amiquinbot

# Check status
docker-compose ps
```

**5c. Production with Monitoring:**

```bash
# Full production stack with monitoring
docker-compose --profile prod-full up -d
```

#### Step 6: Verify Deployment

1. **Check container status:**

   ```bash
   docker-compose ps
   ```

   All containers should show "Up" status.

2. **Check bot logs:**

   ```bash
   docker-compose logs --tail=50 amiquin-bot
   ```

   Look for "Bot is ready" or similar success message.

3. **Test in Discord:**
   - Invite the bot to your server
   - Try a command: `!amq help`
   - Test AI chat by mentioning the bot

#### Step 7: Post-Deployment Maintenance

**View logs:**

```bash
# Follow live logs
docker-compose logs -f amiquin-bot

# Last 100 lines
docker-compose logs --tail=100 amiquin-bot
```

**Restart services:**

```bash
# Restart bot only
docker-compose restart amiquin-bot

# Restart all services
docker-compose restart
```

**Update to latest version:**

```bash
# Pull latest changes
git pull origin main

# Rebuild and restart
docker-compose build --no-cache amiquin-bot
docker-compose up -d
```

**Stop services:**

```bash
# Stop all services
docker-compose --profile prod down

# Stop and remove volumes (⚠️ deletes data)
docker-compose --profile prod down -v
```

**Backup database:**

```bash
# MySQL backup
docker exec mysql-amiquin mysqldump -u root -p amiquin_db > backup_$(date +%Y%m%d).sql

# Qdrant backup
docker exec qdrant-amiquin tar czf /qdrant/snapshots/backup.tar.gz /qdrant/storage
```

### Docker Compose Profiles Reference

The project supports multiple deployment profiles for different use cases:

| Profile | Services | Use Case |
|---------|----------|----------|
| `qdrant-only` | Qdrant only | Testing memory system |
| `database` | MySQL only | Testing database |
| `dev` | Bot + MySQL + Qdrant + Web UI | Full development environment |
| `prod` | Bot + MySQL + Qdrant | Production deployment |
| `prod-full` | All + monitoring | Production with monitoring |

### Environment Variables Reference

**Required:**

- `AMQ_Discord__Token` - Your Discord bot token
- `AMQ_LLM__Providers__OpenAI__ApiKey` - OpenAI API key (or alternative LLM)

**Database:**

- `AMQ_Database__Mode` - `0` for MySQL, `1` for SQLite (default)
- `AMQ_ConnectionStrings__Amiquin-Mysql` - MySQL connection string (if using MySQL)

**Optional Features:**

- `AMQ_Memory__Enabled=true` - Enable AI memory system with Qdrant
- `AMQ_WebSearch__Enabled=true` - Enable web search in ReAct reasoning
- `AMQ_WebSearch__Provider=DuckDuckGo` - Search provider (DuckDuckGo/Google/Bing)
- `AMQ_Voice__Enabled=true` - Enable voice/TTS features

**Security (Production):**

- `AMQ_DB_ROOT_PASSWORD` - MySQL root password (auto-generated)
- `AMQ_DB_USER_PASSWORD` - MySQL user password (auto-generated)
- `AMQ_QDRANT_API_KEY` - Qdrant API key for authentication (optional)

For complete configuration options, see [DEPLOYMENT.md](DEPLOYMENT.md) or `.env.example`.

### Troubleshooting

**Bot won't start:**

1. Check logs: `docker-compose logs amiquin-bot`
2. Verify Discord token in `.env`
3. Ensure OpenAI API key is set
4. Run the checklist: `./scripts/pre-deployment-checklist.ps1`

**Database connection failed:**

1. Check MySQL container: `docker-compose ps mysql`
2. Verify credentials in `.env` match container settings
3. Wait for MySQL health check to pass (60s on first start)

**Memory system not working:**

1. Check Qdrant container: `docker-compose ps qdrant`
2. Verify `AMQ_Memory__Enabled=true` in `.env`
3. Check host is set to `qdrant` (not `localhost`) in Docker

**Web search failing:**

- For DuckDuckGo: No setup needed, should work out of the box
- For Google: Verify API key and Search Engine ID are set
- For Bing: Verify API key is set and valid

For detailed troubleshooting, see [DEPLOYMENT.md](DEPLOYMENT.md).

### ⚙️ Configuration

The application uses `appsettings.json` and `.env` files for configuration. Ensure those files exist.
The templates are provided via `appsettings.example.json` and `.env.example`.

You can override settings using command-line arguments.

Required parameters:

- `Bot:Token` (appsettings.json) or `BOT_TOKEN` (.env) - Discord bot token.
- `Bot:OpenAIKey` (appsettings.json) or `OPEN_AI_KEY` (.env) - OpenAI API key.

> **Note:** the `.env` file is used only for docker-compose configuration.

## 📜 Logging

Logs are written to the console and a rolling log file located in the directory specified by `SQLITE_PATH` environment variable or in the application root `/Logs` directory.

## ☁️ Project Structure

For detailed information about the project architecture, components, and structure, please refer to the [Architecture Documentation](https://huebyte.github.io/Amiquin/architecture.html).

## 🫂 Contributing

Contributions are welcome! Please fork the repository and submit a pull request with your changes.

## 📄 GitHub Pages Documentation

This project uses GitHub Pages to host its documentation. The documentation is automatically built and deployed from the `docs/` folder.
For this project: `https://huebyte.github.io/Amiquin/`

### Updating Documentation

The documentation is written in Markdown and located in the `docs/` folder:

- `docs/index.md` - Main documentation homepage
- `docs/getting-started.md` - Installation and setup guide
- `docs/commands.md` - Discord commands reference
- `docs/architecture.md` - Technical architecture details
- `docs/development.md` - Development and contribution guide
- `docs/configuration.md` - Configuration options and examples

To update the documentation:

1. Edit the relevant Markdown files in the `docs/` folder
2. Commit and push your changes
3. GitHub Pages will automatically rebuild and deploy the updated documentation

## 🪪 License

This project is licensed under the [MIT License](LICENSE).

## 💖 Acknowledgments

- [Serilog](https://serilog.net/) for robust logging capabilities.
- [SpectreConsole](https://spectreconsole.net/quick-start) for beautiful console output.
- [Discord.NET](https://github.com/discord-net/Discord.Net) for Discord bot integration.
