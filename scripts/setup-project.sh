#!/bin/bash

# Amiquin Project Setup Script for Linux/macOS
# Interactive setup script that configures the Amiquin project

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Parse arguments
HELP=false
NON_INTERACTIVE=false
DEFAULT=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --help|-h)
            HELP=true
            shift
            ;;
        --non-interactive)
            NON_INTERACTIVE=true
            shift
            ;;
        --default)
            DEFAULT=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Show help
if [ "$HELP" = true ]; then
    echo -e "${CYAN}Amiquin Project Setup Script${NC}"
    echo ""
    echo "This script will configure your Amiquin project by:"
    echo "  - Creating configuration files from templates"
    echo "  - Prompting for OpenAI API Key (optional)"
    echo "  - Setting up data directories"
    echo "  - Building the solution"
    echo ""
    echo "Usage:"
    echo "  ./setup-project.sh              # Interactive mode (recommended)"
    echo "  ./setup-project.sh --default    # Interactive with sensible defaults"
    echo "  ./setup-project.sh --non-interactive  # Automated setup with defaults"
    echo "  ./setup-project.sh --help       # Show this help"
    exit 0
fi

echo -e "${CYAN}=== Amiquin Project Setup ===${NC}"
echo ""

# Configuration defaults
BOT_TOKEN=""
BOT_NAME="Amiquin"
OPENAI_API_KEY=""
WAIFU_API_TOKEN=""
CHAT_SYSTEM_MESSAGE="I want you to act as personal assistant called Amiquin. You are friendly, helpful and professional."
CHAT_TOKEN_LIMIT=2000
CHAT_ENABLED="true"
CHAT_MODEL="gpt-4o-mini"
DATABASE_MODE=1  # SQLite by default
DATABASE_CONNECTION="Data Source=Data/Database/amiquin.db"
LOGS_PATH="Data/Logs"
MESSAGES_PATH="Data/Messages"
SESSIONS_PATH="Data/Sessions"
PLUGINS_PATH="Data/Plugins"
CONFIGURATION_PATH="Configuration"

# Docker MySQL configuration (passwords will be generated)
BOT_INSTANCE_NAME="amiquin"
MYSQL_ROOT_PASSWORD=""
MYSQL_DATABASE="amiquin_db"
MYSQL_USER="amiquin_user"
MYSQL_USER_PASSWORD=""

# Voice/TTS
VOICE_ENABLED="true"
TTS_MODEL_NAME="en_GB-northern_english_male-medium"

# Function to create directory
ensure_directory() {
    local dir="$1"
    if [ ! -d "$dir" ]; then
        mkdir -p "$dir"
        echo -e "${GREEN}Created directory: $dir${NC}"
    fi
}

# Function to generate secure string without shell-problematic characters
generate_secure_string() {
    local length=${1:-32}
    # Avoid characters that cause issues in shell/Docker: $ ^ ! & * ` ' " \ | < > ( ) { } [ ] ; space
    # Use only alphanumeric and safe special characters: @ # % _ - + =
    local chars='ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@#%_-+='
    local result=""
    for i in $(seq 1 $length); do
        result="${result}${chars:RANDOM % ${#chars}:1}"
    done
    echo "$result"
}

# Generate MySQL passwords by default for Docker compatibility
MYSQL_ROOT_PASSWORD=$(generate_secure_string 24)
MYSQL_USER_PASSWORD=$(generate_secure_string 24)

# Interactive prompts
if [ "$NON_INTERACTIVE" = true ]; then
    echo -e "${YELLOW}Running in non-interactive mode with default values${NC}"
else
    echo -e "${GREEN}This script will configure your Amiquin project with the necessary settings.${NC}"
    echo -e "Press Enter to use default values shown in [brackets]"
    echo ""
    
    # Discord Bot Configuration
    echo -e "${CYAN}=== Discord Bot Configuration ===${NC}"
    read -p "Enter Discord Bot Token (required) [leave empty to configure later]: " input
    if [ ! -z "$input" ]; then
        BOT_TOKEN="$input"
        echo -e "${GREEN}Discord bot token configured successfully${NC}"
    else
        echo -e "${YELLOW}Discord bot token will need to be configured later${NC}"
    fi
    
    # OpenAI Configuration
    echo -e "\n${CYAN}=== AI Configuration ===${NC}"
    read -p "Enter OpenAI API Key (required for AI features) [leave empty to configure later]: " input
    if [ ! -z "$input" ]; then
        OPENAI_API_KEY="$input"
        echo -e "${GREEN}OpenAI API key configured successfully${NC}"
    else
        echo -e "${YELLOW}OpenAI API key will need to be configured later for AI features to work${NC}"
    fi
    
    # Waifu API Configuration
    echo -e "\n${CYAN}=== NSFW/Waifu API Configuration ===${NC}"
    echo "Waifu API token is optional but recommended for better rate limits and NSFW features"
    echo "Get your token from: https://www.waifu.im/dashboard/"
    read -p "Enter Waifu API Token [leave empty to configure later]: " input
    if [ ! -z "$input" ]; then
        WAIFU_API_TOKEN="$input"
        echo -e "${GREEN}Waifu API token configured successfully${NC}"
    else
        echo -e "${YELLOW}Waifu API token can be configured later in the .env file for better NSFW functionality${NC}"
    fi
    
    # System Message
    if [ "$DEFAULT" = false ]; then
        read -p "Enter AI system message [$CHAT_SYSTEM_MESSAGE]: " input
        if [ ! -z "$input" ]; then
            CHAT_SYSTEM_MESSAGE="$input"
        fi
        
        # Model selection
        echo ""
        echo -e "${CYAN}Select AI Model:${NC}"
        echo "1. gpt-4o-mini (default - faster, cheaper)"
        echo "2. gpt-4o (more capable, more expensive)"
        echo "3. gpt-3.5-turbo (legacy, cheapest)"
        read -p "Enter choice [1]: " model_choice
        
        case $model_choice in
            2)
                CHAT_MODEL="gpt-4o"
                ;;
            3)
                CHAT_MODEL="gpt-3.5-turbo"
                ;;
            *)
                CHAT_MODEL="gpt-4o-mini"
                ;;
        esac
    else
        echo -e "Using default system message for Amiquin"
    fi
    
    # Database configuration
    echo -e "\n${CYAN}=== Database Configuration ===${NC}"
    if [ "$DEFAULT" = false ]; then
        echo "Select Database Type:"
        echo "1. SQLite (default - recommended for development)"
        echo "2. MySQL (for production/Docker deployments)"
        read -p "Enter choice [1]: " db_choice
        
        if [ "$db_choice" = "2" ]; then
            DATABASE_MODE=0
            
            echo -e "${GREEN}Using pre-generated secure MySQL passwords${NC}"
            
            # Ask for database details
            read -p "Enter database name [$MYSQL_DATABASE]: " input
            if [ ! -z "$input" ]; then
                MYSQL_DATABASE="$input"
            fi
            
            read -p "Enter database user [$MYSQL_USER]: " input
            if [ ! -z "$input" ]; then
                MYSQL_USER="$input"
            fi
            
            read -p "Enter bot instance name (for Docker containers) [$BOT_INSTANCE_NAME]: " input
            if [ ! -z "$input" ]; then
                BOT_INSTANCE_NAME="$input"
            fi
            
            # Update MySQL connection string
            DATABASE_CONNECTION="Server=localhost;Database=$MYSQL_DATABASE;Uid=$MYSQL_USER;Pwd=$MYSQL_USER_PASSWORD;Pooling=True;"
            
            echo "MySQL configuration:"
            echo "  Database: $MYSQL_DATABASE"
            echo "  User: $MYSQL_USER"
            echo "  Instance: $BOT_INSTANCE_NAME"
            echo "  Passwords: Already generated securely"
        fi
    else
        # In default mode, MySQL passwords are already generated
        echo -e "${GREEN}MySQL passwords generated for Docker compatibility${NC}"
    fi
fi

# Create .env file
echo ""
echo -e "${CYAN}Creating .env file...${NC}"

ENV_PATH="$PROJECT_ROOT/.env"

cat > "$ENV_PATH" << EOF
# Amiquin Environment Configuration
# Generated by setup script on $(date '+%Y-%m-%d %H:%M:%S')
# All configuration values use AMQ_ prefix as defined in .env.example

# ======================
# Discord Configuration
# ======================
$(if [ ! -z "$BOT_TOKEN" ]; then echo "AMQ_Discord__Token=\"$BOT_TOKEN\""; else echo "# AMQ_Discord__Token=\"your-discord-bot-token-here\""; fi)
AMQ_Discord__Prefix="!amq"
AMQ_Discord__ActivityMessage="Chatting with AI"

# ======================
# Bot Configuration
# ======================
AMQ_Bot__Name="$BOT_NAME"
AMQ_Bot__PrintLogo=false
AMQ_Bot__MessageFetchCount=40
AMQ_Bot__MaxTokens=20000

# ======================
# LLM (AI Language Model) Configuration
# ======================
AMQ_LLM__DefaultProvider="OpenAI"
AMQ_LLM__EnableFallback=true
AMQ_LLM__FallbackOrder__0="OpenAI"
AMQ_LLM__FallbackOrder__1="Grok"
AMQ_LLM__FallbackOrder__2="Gemini"
AMQ_LLM__GlobalSystemMessage="$CHAT_SYSTEM_MESSAGE"
AMQ_LLM__GlobalTemperature=0.6
AMQ_LLM__GlobalTimeout=120

# OpenAI Provider Configuration
AMQ_LLM__Providers__OpenAI__Enabled=true
$(if [ ! -z "$OPENAI_API_KEY" ]; then echo "AMQ_LLM__Providers__OpenAI__ApiKey=\"$OPENAI_API_KEY\""; else echo "# AMQ_LLM__Providers__OpenAI__ApiKey=\"sk-your-openai-api-key-here\""; fi)
AMQ_LLM__Providers__OpenAI__BaseUrl="https://api.openai.com/v1/"
AMQ_LLM__Providers__OpenAI__DefaultModel="$CHAT_MODEL"

# OpenAI Model Configurations
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__Name="GPT-4 Omni"
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__MaxTokens=128000
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__MaxOutputTokens=4096

AMQ_LLM__Providers__OpenAI__Models__gpt-4o-mini__Name="GPT-4 Omni Mini"
AMQ_LLM__Providers__OpenAI__Models__gpt-4o-mini__MaxTokens=128000
AMQ_LLM__Providers__OpenAI__Models__gpt-4o-mini__MaxOutputTokens=16384

# ======================
# Database Configuration
# ======================
AMQ_Database__Mode=$DATABASE_MODE

# Provider-specific Connection Strings (Recommended)
$(if [ "$DATABASE_MODE" = "1" ]; then
    echo "AMQ_ConnectionStrings__Amiquin-Sqlite=\"Data Source=Data/Database/amiquin.db\""
    echo "# AMQ_ConnectionStrings__Amiquin-Mysql=\"Server=localhost;Database=amiquin_db;Uid=amiquin_user;Pwd=amiquin_password;Pooling=True;\""
else
    echo "# AMQ_ConnectionStrings__Amiquin-Sqlite=\"Data Source=Data/Database/amiquin.db\""
    echo "AMQ_ConnectionStrings__Amiquin-Mysql=\"$DATABASE_CONNECTION\""
fi)

# Legacy Connection String (for backward compatibility)
AMQ_ConnectionStrings__AmiquinContext="$DATABASE_CONNECTION"

# ======================
# Docker MySQL Configuration (for docker-compose)
# ======================
AMQ_BOT_NAME="$BOT_INSTANCE_NAME"
AMQ_DB_ROOT_PASSWORD="$MYSQL_ROOT_PASSWORD"
AMQ_DB_NAME="$MYSQL_DATABASE"
AMQ_DB_USER="$MYSQL_USER"
AMQ_DB_USER_PASSWORD="$MYSQL_USER_PASSWORD"

# ======================
# Data Paths Configuration
# ======================
AMQ_DataPaths__Logs="$LOGS_PATH"
AMQ_DataPaths__Messages="$MESSAGES_PATH"
AMQ_DataPaths__Sessions="$SESSIONS_PATH"
AMQ_DataPaths__Plugins="$PLUGINS_PATH"
AMQ_DataPaths__Configuration="$CONFIGURATION_PATH"

# ======================
# Voice/TTS Configuration
# ======================
AMQ_Voice__TTSModelName="$TTS_MODEL_NAME"
AMQ_Voice__PiperCommand="/usr/local/bin/piper"
AMQ_Voice__Enabled=$VOICE_ENABLED

# ======================
# NSFW/Waifu API Configuration
# ======================
$(if [ ! -z "$WAIFU_API_TOKEN" ]; then echo "AMQ_WaifuApi__Token=\"$WAIFU_API_TOKEN\""; else echo "# AMQ_WaifuApi__Token=\"your-waifu-api-token-here\""; fi)
AMQ_WaifuApi__BaseUrl="https://api.waifu.im"
AMQ_WaifuApi__Version="v5"
AMQ_WaifuApi__Enabled=true

# ======================
# Logging Configuration
# ======================
AMQ_Serilog__MinimumLevel__Default="Information"
AMQ_Serilog__MinimumLevel__Override__System="Warning"
AMQ_Serilog__MinimumLevel__Override__Microsoft="Warning"
AMQ_Serilog__MinimumLevel__Override__Discord="Information"

# ======================
# Optional Providers (Disabled by default)
# ======================
# Grok Provider Configuration
# AMQ_LLM__Providers__Grok__Enabled=false
# AMQ_LLM__Providers__Grok__ApiKey="xai-your-grok-api-key-here"
# AMQ_LLM__Providers__Grok__BaseUrl="https://api.x.ai/v1/"
# AMQ_LLM__Providers__Grok__DefaultModel="grok-3"

# Gemini Provider Configuration
# AMQ_LLM__Providers__Gemini__Enabled=false
# AMQ_LLM__Providers__Gemini__ApiKey="your-gemini-api-key-here"
# AMQ_LLM__Providers__Gemini__BaseUrl="https://generativelanguage.googleapis.com/"
# AMQ_LLM__Providers__Gemini__DefaultModel="gemini-1.5-flash"
EOF

echo -e "${GREEN}Created .env file${NC}"

# Create Configuration directory and appsettings.json
echo -e "${CYAN}Creating appsettings.json...${NC}"

CONFIG_DIR="$PROJECT_ROOT/source/Amiquin.Bot/Configuration"
ensure_directory "$CONFIG_DIR"

APPSETTINGS_PATH="$CONFIG_DIR/appsettings.json"

# Create appsettings.json
if [ ! -z "$OPENAI_API_KEY" ]; then
    AUTH_TOKEN_VALUE="\"$OPENAI_API_KEY\""
else
    AUTH_TOKEN_VALUE="\"your-openai-api-key\""
fi

cat > "$APPSETTINGS_PATH" << EOF
{
  "Chat": {
    "AuthToken": $AUTH_TOKEN_VALUE,
    "SystemMessage": "$CHAT_SYSTEM_MESSAGE",
    "TokenLimit": $CHAT_TOKEN_LIMIT,
    "Enabled": $CHAT_ENABLED,
    "Model": "$CHAT_MODEL"
  },
  "ConnectionStrings": {
    "AmiquinContext": "$DATABASE_CONNECTION",
    "Amiquin-Sqlite": "Data Source=Data/Database/amiquin.db",
    "Amiquin-Mysql": "Server=localhost;Database=amiquin;User=amiquin;Password=your_password;Pooling=True;"
  },
  "DataPaths": {
    "Logs": "$LOGS_PATH",
    "Messages": "$MESSAGES_PATH",
    "Sessions": "$SESSIONS_PATH",
    "Plugins": "$PLUGINS_PATH",
    "Configuration": "$CONFIGURATION_PATH"
  },
  "SessionManagement": {
    "MaxSessionsPerUser": 5,
    "InactivityTimeoutMinutes": 120,
    "CleanupIntervalMinutes": 30,
    "MaxHistoryLength": 50
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "System": "Warning",
        "Microsoft": "Warning",
        "Discord": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "$LOGS_PATH/amiquin-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithThreadId", "WithEnvironmentName"],
    "Properties": {
      "Application": "Amiquin"
    }
  }
}
EOF

echo -e "${GREEN}Created appsettings.json${NC}"

# Create data directories
echo ""
echo -e "${CYAN}Creating data directories...${NC}"

DATA_DIR="$PROJECT_ROOT/Data"
ensure_directory "$DATA_DIR"
ensure_directory "$DATA_DIR/Logs"
ensure_directory "$DATA_DIR/Database"
ensure_directory "$DATA_DIR/Messages"
ensure_directory "$DATA_DIR/Sessions"
ensure_directory "$DATA_DIR/Plugins"

# Check if solution exists and build
SOLUTION_PATH="$PROJECT_ROOT/source/source.sln"
if [ -f "$SOLUTION_PATH" ]; then
    echo ""
    echo -e "${CYAN}Building solution...${NC}"
    
    cd "$PROJECT_ROOT/source"
    
    # Restore dependencies
    echo -e "${CYAN}Restoring NuGet packages...${NC}"
    dotnet restore source.sln
    
    # Build solution
    dotnet build source.sln --configuration Release
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}Solution built successfully${NC}"
    else
        echo -e "${RED}Build failed. Please check the errors above.${NC}"
    fi
else
    echo -e "${YELLOW}Solution file not found at expected location: $SOLUTION_PATH${NC}"
fi

# Summary
echo ""
echo -e "${GREEN}=== Setup Complete ===${NC}"
echo ""
echo -e "${CYAN}Configuration files created:${NC}"
echo "  - .env (Environment variables)"
echo "  - source/Amiquin.Bot/Configuration/appsettings.json (Application configuration)"
echo "  - source/Amiquin.Bot/Configuration/appsettings.example.json (Template for other developers)"
echo ""
echo -e "${CYAN}Data directories created:${NC}"
echo "  - Data/Logs (Application logs)"
echo "  - Data/Database (SQLite database)"
echo "  - Data/Messages (Message storage)"
echo "  - Data/Sessions (Session storage)"
echo "  - Data/Plugins (Plugin storage)"
echo ""

# Show warnings for missing configuration
HAS_WARNINGS=false

if [ -z "$BOT_TOKEN" ]; then
    if [ "$HAS_WARNINGS" = false ]; then
        echo -e "${YELLOW}IMPORTANT: Missing configuration${NC}"
        echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        HAS_WARNINGS=true
    fi
    echo -e "${YELLOW}  • Discord Bot Token: Required for bot functionality${NC}"
    echo "    - Update 'AMQ_Discord__Token' in .env file"
fi

if [ -z "$OPENAI_API_KEY" ]; then
    if [ "$HAS_WARNINGS" = false ]; then
        echo -e "${YELLOW}IMPORTANT: Missing configuration${NC}"
        echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        HAS_WARNINGS=true
    fi
    echo -e "${YELLOW}  • OpenAI API Key: Required for AI chat features${NC}"
    echo "    - Update 'AMQ_LLM__Providers__OpenAI__ApiKey' in .env file"
fi

if [ "$HAS_WARNINGS" = true ]; then
    echo ""
fi

echo -e "${YELLOW}Next steps:${NC}"

STEP_NUMBER=1

if [ -z "$BOT_TOKEN" ]; then
    echo "$STEP_NUMBER. Add your Discord Bot Token to .env file"
    echo "   - Get token from: https://discord.com/developers/applications"
    ((STEP_NUMBER++))
fi

if [ -z "$OPENAI_API_KEY" ]; then
    echo "$STEP_NUMBER. Add your OpenAI API key to .env file (for AI features)"
    echo "   - Get key from: https://platform.openai.com/api-keys"
    ((STEP_NUMBER++))
fi

if [ -f "$SOLUTION_PATH" ]; then
    echo "$STEP_NUMBER. Run database migrations (if needed):"
    echo "   dotnet ef database update -p source/Amiquin.Infrastructure -s source/Amiquin.Bot"
    ((STEP_NUMBER++))
    
    echo "$STEP_NUMBER. Start the application:"
    echo "   cd source/Amiquin.Bot && dotnet run"
else
    echo "$STEP_NUMBER. Check that the solution exists at: source/source.sln"
fi

echo ""
echo "All configuration values can be overridden using environment variables with AMQ_ prefix."
echo "For more information, see the documentation at dev/docs/"
echo ""