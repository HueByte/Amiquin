# Installation Guide

This guide covers different ways to install and deploy Amiquin.

## For Discord Server Owners

### Option 1: Invite Public Bot (Recommended)

The easiest way to get Amiquin in your server:

1. **Use the invite link**: [Add Amiquin to your server](https://discord.com/oauth2/authorize?client_id=YOUR_BOT_ID&permissions=8&scope=bot)
2. **Select your server** from the dropdown
3. **Review permissions** and click "Authorize"
4. **Verify** the bot appears in your member list

### Required Permissions

Amiquin needs these permissions to function properly:

- **Send Messages** - To respond to commands
- **Embed Links** - To send rich message embeds
- **Use Slash Commands** - For modern Discord commands
- **Read Message History** - For context-aware features
- **Manage Messages** - For moderation features (optional)
- **Connect & Speak** - For voice features (optional)

## For Self-Hosting

### Option 2: Docker (Recommended for Self-Hosting)

Run Amiquin using Docker:

```bash
# Pull the latest image
docker pull ghcr.io/huebyte/amiquin:latest

# Run with environment variables
docker run -d \
  --name amiquin \
  -e DISCORD_TOKEN=your_bot_token \
  -e DATABASE_CONNECTION=your_db_connection \
  ghcr.io/huebyte/amiquin:latest
```

### Option 3: Docker Compose

Use the provided `docker-compose.yml`:

```bash
# Copy environment file
cp .env.example .env

# Edit environment variables
nano .env

# Start the services
docker-compose up -d
```

### Option 4: Manual Installation

For development or custom deployments:

#### Prerequisites

- .NET 9.0 SDK
- Git
- Database (MySQL/SQLite)

#### Steps

1. **Clone the repository**:
   ```bash
   git clone https://github.com/huebyte/Amiquin.git
   cd Amiquin
   ```

2. **Configure settings**:
   ```bash
   cp source/Amiquin.Bot/appsettings.example.json source/Amiquin.Bot/appsettings.json
   # Edit appsettings.json with your configuration
   ```

3. **Build and run**:
   ```bash
   dotnet restore
   dotnet build
   dotnet run --project source/Amiquin.Bot
   ```

## Environment Configuration

### Required Environment Variables

```env
# Discord Bot Token (Required)
DISCORD_TOKEN=your_discord_bot_token

# Database Configuration
DATABASE_TYPE=sqlite  # or mysql
DATABASE_CONNECTION=Data Source=amiquin.db

# Optional Configuration
LOG_LEVEL=Information
ENVIRONMENT=Production
```

### Creating a Discord Bot

To get a Discord bot token:

1. Go to [Discord Developer Portal](https://discord.com/developers/applications)
2. Click "New Application"
3. Navigate to "Bot" section
4. Click "Add Bot"
5. Copy the token from "Token" section
6. Enable necessary Privileged Gateway Intents if needed

## Database Setup

### SQLite (Default)

No additional setup required. The database file will be created automatically.

### MySQL

1. **Create database**:
   ```sql
   CREATE DATABASE amiquin;
   CREATE USER 'amiquin'@'%' IDENTIFIED BY 'your_password';
   GRANT ALL PRIVILEGES ON amiquin.* TO 'amiquin'@'%';
   ```

2. **Update connection string**:
   ```env
   DATABASE_TYPE=mysql
   DATABASE_CONNECTION=Server=localhost;Database=amiquin;User=amiquin;Password=your_password;
   ```

## Verification

After installation, verify Amiquin is working:

1. **Check bot status** - Bot should appear online in Discord
2. **Test basic command** - Try `/ping` or `/help`
3. **Check logs** - Review application logs for any errors

## Troubleshooting

### Common Issues

- **Bot appears offline**: Check Discord token and internet connection
- **Commands not working**: Verify bot permissions and slash command registration
- **Database errors**: Check database connection string and permissions

### Getting Help

- Check [configuration guide](configuration.md)
- Join our [Discord server](https://discord.gg/your-invite-link)
- Create an [issue on GitHub](https://github.com/huebyte/Amiquin/issues)
