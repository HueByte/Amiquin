#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Setup script for Amiquin project configuration
.DESCRIPTION
    Interactive setup script that prompts for critical configuration values and sets up the Amiquin project
#>

param(
    [switch]$Help,
    [switch]$NonInteractive,
    [switch]$Default,
    [switch]$Production
)

if ($Help) {
    Write-Host "Amiquin Project Setup Script" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "This script will configure your Amiquin project by:"
    Write-Host "  - Creating configuration files from templates"
    Write-Host "  - Prompting for OpenAI API Key (required for AI features)"
    Write-Host "  - Configuring memory system with Qdrant vector database"
    Write-Host "  - Setting up data directories"
    Write-Host "  - Building the solution"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  ./setup-project.ps1                # Interactive mode (recommended)"
    Write-Host "  ./setup-project.ps1 -Default       # Interactive with sensible defaults"
    Write-Host "  ./setup-project.ps1 -Production    # Production mode with security hardening"
    Write-Host "  ./setup-project.ps1 -NonInteractive # Automated setup with defaults"
    Write-Host "  ./setup-project.ps1 -Help          # Show this help"
    exit 0
}

Write-Host "=== Amiquin Project Setup ===" -ForegroundColor Cyan
Write-Host ""

# Configuration values aligned with current AMQ_ prefix system
$config = @{
    # Bot configuration
    BotToken           = ""
    BotName            = "Amiquin"
    
    # LLM configuration
    LLMProvider        = "OpenAI"
    OpenAIApiKey       = ""
    SystemMessage      = "I want you to act as personal assistant called Amiquin. You are friendly, helpful and professional."
    DefaultModel       = "gpt-4o-mini"
    
    # NSFW/Waifu API configuration
    WaifuApiToken      = ""
    
    # Database
    DatabaseMode       = 1  # SQLite by default
    DatabaseConnection = "Data Source=Data/Database/amiquin.db"
    
    # Docker MySQL configuration (passwords will be generated later)
    BotInstanceName    = "amiquin-instance"
    MySQLRootPassword  = ""
    MySQLDatabase      = "amiquin_db"
    MySQLUser          = "amiquin_user"
    MySQLUserPassword  = ""
    
    # Qdrant configuration (API key will be generated if needed)
    QdrantApiKey       = ""
    QdrantEnableAuth   = $false
    QdrantHost         = "localhost"
    QdrantPort         = "6334"
    
    # Data paths
    LogsPath           = "Data/Logs"
    MessagesPath       = "Data/Messages"
    SessionsPath       = "Data/Sessions"
    PluginsPath        = "Data/Plugins"
    ConfigurationPath  = "Configuration"
    
    # Voice/TTS
    VoiceEnabled       = $true
    TTSModelName       = "en_GB-northern_english_male-medium"
    
    # Web Search
    WebSearchEnabled   = $false
    WebSearchProvider  = "DuckDuckGo"
    WebSearchApiKey    = ""
    WebSearchEngineId  = ""
}

# Generate secure defaults without shell-problematic characters
function New-SecureString {
    param([int]$Length = 32)
    # Avoid characters that cause issues in shell/Docker: $ ^ ! & * ` ' " \ | < > ( ) { } [ ] ; space
    # Use only alphanumeric and safe special characters: @ # % _ - + = 
    $chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@#%_-+='
    $random = New-Object System.Random
    $result = ""
    for ($i = 0; $i -lt $Length; $i++) {
        $result += $chars[$random.Next($chars.Length)]
    }
    return $result
}

# Create directories
function Ensure-Directory {
    param([string]$Path)
    if (!(Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
        Write-Host "Created directory: $Path" -ForegroundColor Green
    }
}

# Generate MySQL passwords by default for Docker compatibility
$config.MySQLRootPassword = New-SecureString -Length 24
$config.MySQLUserPassword = New-SecureString -Length 24

if ($NonInteractive) {
    Write-Host "Running in non-interactive mode with default values" -ForegroundColor Yellow
}
else {
    Write-Host "This script will configure your Amiquin project with the necessary settings." -ForegroundColor Green
    Write-Host "Press Enter to use default values shown in [brackets]" -ForegroundColor Gray
    Write-Host ""
    
    # Discord Bot Configuration
    Write-Host "=== Discord Bot Configuration ===" -ForegroundColor Cyan
    $botToken = Read-Host "Enter Discord Bot Token (required) [leave empty to configure later]"
    if ($botToken) { 
        $config.BotToken = $botToken 
        Write-Host "Discord bot token configured successfully" -ForegroundColor Green
    }
    else {
        Write-Host "Discord bot token will need to be configured later" -ForegroundColor Yellow
    }
    
    # OpenAI Configuration
    Write-Host "\n=== AI Configuration ===" -ForegroundColor Cyan
    $openAIKey = Read-Host "Enter OpenAI API Key (required for AI features) [leave empty to configure later]"
    if ($openAIKey) { 
        $config.OpenAIApiKey = $openAIKey 
        Write-Host "OpenAI API key configured successfully" -ForegroundColor Green
    }
    else {
        Write-Host "OpenAI API key will need to be configured later for AI features to work" -ForegroundColor Yellow
    }
    
    # Waifu API Configuration
    Write-Host "\n=== NSFW/Waifu API Configuration ===" -ForegroundColor Cyan
    Write-Host "Waifu API token is optional but recommended for better rate limits and NSFW features"
    Write-Host "Get your token from: https://www.waifu.im/dashboard/"
    $waifuToken = Read-Host "Enter Waifu API Token [leave empty to configure later]"
    if ($waifuToken) { 
        $config.WaifuApiToken = $waifuToken 
        Write-Host "Waifu API token configured successfully" -ForegroundColor Green
    }
    else {
        Write-Host "Waifu API token can be configured later in the .env file for better NSFW functionality" -ForegroundColor Yellow
    }
    
    # Web Search Configuration
    Write-Host "\n=== Web Search Configuration ===" -ForegroundColor Cyan
    Write-Host "Web search allows the bot to look up current information during conversations"
    Write-Host "Provider options: DuckDuckGo (free, no API key), Google, Bing"
    if (!$Default) {
        Write-Host "Enable web search? (y/N)"
        $enableSearch = Read-Host "Enter choice [N]"
        if ($enableSearch -eq "y" -or $enableSearch -eq "Y") {
            $config.WebSearchEnabled = $true
            
            Write-Host "Select provider:"
            Write-Host "1. DuckDuckGo (default - no API key required)"
            Write-Host "2. Google Custom Search (requires API key + Search Engine ID)"
            Write-Host "3. Bing Search API (requires API key)"
            $providerChoice = Read-Host "Enter choice [1]"
            
            switch ($providerChoice) {
                "2" {
                    $config.WebSearchProvider = "Google"
                    $searchApiKey = Read-Host "Enter Google API key"
                    if ($searchApiKey) { $config.WebSearchApiKey = $searchApiKey }
                    $searchEngineId = Read-Host "Enter Google Search Engine ID"
                    if ($searchEngineId) { $config.WebSearchEngineId = $searchEngineId }
                }
                "3" {
                    $config.WebSearchProvider = "Bing"
                    $searchApiKey = Read-Host "Enter Bing API key"
                    if ($searchApiKey) { $config.WebSearchApiKey = $searchApiKey }
                }
                default {
                    $config.WebSearchProvider = "DuckDuckGo"
                    Write-Host "Using DuckDuckGo (no API key required)" -ForegroundColor Green
                }
            }
        }
    }
    
    # System Message
    if ($Default) {
        Write-Host "Using default system message for Amiquin" -ForegroundColor Gray
    }
    else {
        $systemMessage = Read-Host "Enter AI system message [$($config.SystemMessage)]"
        if ($systemMessage) { $config.SystemMessage = $systemMessage }
    }
    
    # Vector Database (Qdrant) Configuration
    Write-Host "`n=== Vector Database (Qdrant) Configuration ===" -ForegroundColor Cyan
    Write-Host "Qdrant is used for AI memory system and conversation context"
    if (!$Default) {
        Write-Host "Enable Qdrant authentication? (Optional - for production/cloud deployments)"
        Write-Host "1. No authentication (default - recommended for local development)"
        Write-Host "2. Enable API key authentication (for production)"
        $qdrantChoice = Read-Host "Enter choice [1]"
        
        if ($qdrantChoice -eq "2") {
            $config.QdrantEnableAuth = $true
            
            Write-Host "Choose API key option:"
            Write-Host "1. Generate secure API key automatically (recommended)"
            Write-Host "2. Enter custom API key"
            $keyChoice = Read-Host "Enter choice [1]"
            
            if ($keyChoice -eq "2") {
                $qdrantKey = Read-Host "Enter Qdrant API key"
                if ($qdrantKey) { $config.QdrantApiKey = $qdrantKey }
            }
            else {
                $config.QdrantApiKey = New-SecureString -Length 32
                Write-Host "Generated secure Qdrant API key" -ForegroundColor Green
            }
        }
        
        # Qdrant connection details
        $qdrantHost = Read-Host "Enter Qdrant host [$($config.QdrantHost)]"
        if ($qdrantHost) { $config.QdrantHost = $qdrantHost }
        
        $qdrantPort = Read-Host "Enter Qdrant port [$($config.QdrantPort)]"
        if ($qdrantPort) { $config.QdrantPort = $qdrantPort }
        
        Write-Host "Qdrant configuration:"
        Write-Host "  Host: $($config.QdrantHost)"
        Write-Host "  Port: $($config.QdrantPort)"
        Write-Host "  Authentication: $($config.QdrantEnableAuth)"
        if ($config.QdrantEnableAuth) {
            Write-Host "  API Key: Generated securely"
        }
    }
    else {
        Write-Host "Using default Qdrant configuration (no authentication)" -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "To start Qdrant for development:"
    Write-Host "  docker-compose --profile qdrant-only up -d"
    Write-Host "  # Or: docker-compose --profile vector up -d"
    
    # Database configuration
    Write-Host "\n=== Database Configuration ===" -ForegroundColor Cyan
    if (!$Default) {
        Write-Host "Select Database Type:"
        Write-Host "1. SQLite (default - recommended for development)"
        Write-Host "2. MySQL (for production/Docker deployments)"
        $dbChoice = Read-Host "Enter choice [1]"
        
        if ($dbChoice -eq "2") {
            $config.DatabaseMode = 0
            
            # MySQL passwords already generated in defaults
            Write-Host "Using pre-generated secure MySQL passwords" -ForegroundColor Green
            
            # Ask for database details
            $dbName = Read-Host "Enter database name [$($config.MySQLDatabase)]"
            if ($dbName) { $config.MySQLDatabase = $dbName }
            
            $dbUser = Read-Host "Enter database user [$($config.MySQLUser)]"
            if ($dbUser) { $config.MySQLUser = $dbUser }
            
            $instanceName = Read-Host "Enter bot instance name (for Docker containers) [$($config.BotInstanceName)]"
            if ($instanceName) { $config.BotInstanceName = $instanceName }
            
            # Update MySQL connection string
            $config.DatabaseConnection = "Server=localhost;Database=$($config.MySQLDatabase);Uid=$($config.MySQLUser);Pwd=$($config.MySQLUserPassword);Pooling=True;"
            
            Write-Host "MySQL configuration:"
            Write-Host "  Database: $($config.MySQLDatabase)"
            Write-Host "  User: $($config.MySQLUser)"
            Write-Host "  Instance: $($config.BotInstanceName)"
            Write-Host "  Passwords: Already generated securely"
        }
    }
    else {
        # In default mode, MySQL passwords are already generated in config defaults
        Write-Host "MySQL passwords generated for Docker compatibility" -ForegroundColor Green
    }
}
    
# Model selection
if (!$NonInteractive -and !$Default) {
    Write-Host "\n=== AI Model Selection ===" -ForegroundColor Cyan
    Write-Host "1. gpt-4o-mini (default - faster, cheaper)"
    Write-Host "2. gpt-4o (more capable, more expensive)"
    Write-Host "3. gpt-3.5-turbo (legacy, cheapest)"
    $modelChoice = Read-Host "Enter choice [1]"
        
    switch ($modelChoice) {
        "2" { $config.DefaultModel = "gpt-4o" }
        "3" { $config.DefaultModel = "gpt-3.5-turbo" }
        default { $config.DefaultModel = "gpt-4o-mini" }
    }
}

# Create .env file
Write-Host ""
Write-Host "Creating .env file..." -ForegroundColor Cyan

$envContent = @"
# Amiquin Environment Configuration
# Generated by setup script on $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
# All configuration values use AMQ_ prefix as defined in .env.example

# ======================
# Discord Configuration
# ======================
$(if ($config.BotToken) { "AMQ_Discord__Token=`"$($config.BotToken)`"" } else { "# AMQ_Discord__Token=`"your-discord-bot-token-here`"" })
AMQ_Discord__Prefix=`"!amq`"
AMQ_Discord__ActivityMessage=`"Chatting with AI`"

# ======================
# Bot Configuration
# ======================
AMQ_Bot__Name=`"$($config.BotName)`"
AMQ_Bot__PrintLogo=false
AMQ_Bot__MessageFetchCount=40
AMQ_Bot__ConversationTokenLimit=40000

# ======================
# LLM (AI Language Model) Configuration
# ======================
AMQ_LLM__DefaultProvider=`"$($config.LLMProvider)`"
AMQ_LLM__EnableFallback=true
AMQ_LLM__FallbackOrder__0=`"OpenAI`"
AMQ_LLM__FallbackOrder__1=`"Grok`"
AMQ_LLM__FallbackOrder__2=`"Gemini`"
AMQ_LLM__GlobalSystemMessage=`"$($config.SystemMessage)`"
AMQ_LLM__GlobalTemperature=0.6
AMQ_LLM__GlobalTimeout=120

# OpenAI Provider Configuration
AMQ_LLM__Providers__OpenAI__Enabled=true
$(if ($config.OpenAIApiKey) { "AMQ_LLM__Providers__OpenAI__ApiKey=`"$($config.OpenAIApiKey)`"" } else { "# AMQ_LLM__Providers__OpenAI__ApiKey=`"sk-your-openai-api-key-here`"" })
AMQ_LLM__Providers__OpenAI__BaseUrl=`"https://api.openai.com/v1/`"
AMQ_LLM__Providers__OpenAI__DefaultModel=`"$($config.DefaultModel)`"

# OpenAI Model Configurations
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__Name=`"GPT-4 Omni`"
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__MaxTokens=128000
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__MaxOutputTokens=4096

AMQ_LLM__Providers__OpenAI__Models__gpt-4o-mini__Name=`"GPT-4 Omni Mini`"
AMQ_LLM__Providers__OpenAI__Models__gpt-4o-mini__MaxTokens=128000
AMQ_LLM__Providers__OpenAI__Models__gpt-4o-mini__MaxOutputTokens=16384

# ======================
# Database Configuration
# ======================
AMQ_Database__Mode=$($config.DatabaseMode)

# Provider-specific Connection Strings (Recommended)
$(if ($config.DatabaseMode -eq 1) { 
    "AMQ_ConnectionStrings__Amiquin-Sqlite=`"Data Source=Data/Database/amiquin.db`""
    "# AMQ_ConnectionStrings__Amiquin-Mysql=`"Server=localhost;Database=amiquin_db;Uid=amiquin_user;Pwd=amiquin_password;Pooling=True;`""
} else { 
    "# AMQ_ConnectionStrings__Amiquin-Sqlite=`"Data Source=Data/Database/amiquin.db`""
    "AMQ_ConnectionStrings__Amiquin-Mysql=`"$($config.DatabaseConnection)`""
})

# Legacy Connection String (for backward compatibility)
$(if ($config.DatabaseMode -eq 1) { "AMQ_ConnectionStrings__AmiquinContext=`"Data Source=Data/Database/amiquin.db`"" } else { "AMQ_ConnectionStrings__AmiquinContext=`"$($config.DatabaseConnection)`"" })

# ======================
# Docker MySQL Configuration (for docker-compose)
# ======================
AMQ_BOT_NAME=`"$($config.BotInstanceName)`"
AMQ_DB_ROOT_PASSWORD=`"$($config.MySQLRootPassword)`"
AMQ_DB_NAME=`"$($config.MySQLDatabase)`"
AMQ_DB_USER=`"$($config.MySQLUser)`"
AMQ_DB_USER_PASSWORD=`"$($config.MySQLUserPassword)`"

# ======================
# Data Paths Configuration
# ======================
AMQ_DataPaths__Logs=`"$($config.LogsPath)`"
AMQ_DataPaths__Messages=`"$($config.MessagesPath)`"
AMQ_DataPaths__Sessions=`"$($config.SessionsPath)`"
AMQ_DataPaths__Plugins=`"$($config.PluginsPath)`"
AMQ_DataPaths__Configuration=`"$($config.ConfigurationPath)`"

# ======================
# Configuration File Path
# ======================
# Path to appsettings.json file - defaults to {AppDirectory.BaseDirectory}/Data/Configuration/appsettings.json
# For Docker volumes, set to /app/Data/Configuration/appsettings.json
AMQ_APPSETTINGS_PATH=`"/app/Data/Configuration/appsettings.json`"

# ======================
# Voice/TTS Configuration
# ======================
AMQ_Voice__TTSModelName=`"$($config.TTSModelName)`"
AMQ_Voice__PiperCommand=`"/usr/local/bin/piper`"
AMQ_Voice__Enabled=$($config.VoiceEnabled.ToString().ToLower())

# ======================
# Memory & Vector Database Configuration
# ======================

# Memory System Settings
AMQ_Memory__Enabled=true
AMQ_Memory__MaxMemoriesPerSession=1000
AMQ_Memory__MaxContextMemories=10
AMQ_Memory__SimilarityThreshold=0.7
AMQ_Memory__MinImportanceScore=0.3
AMQ_Memory__MinMessagesForMemory=3
AMQ_Memory__AutoCleanup=true
AMQ_Memory__CleanupOlderThanDays=30
AMQ_Memory__EmbeddingModel=`"text-embedding-3-small`"

# Qdrant Vector Database Configuration
AMQ_Memory__Qdrant__Host=`"$($config.QdrantHost)`"
AMQ_Memory__Qdrant__Port=$($config.QdrantPort)
AMQ_Memory__Qdrant__UseHttps=false
AMQ_Memory__Qdrant__CollectionName=`"amiquin_memories`"
AMQ_Memory__Qdrant__VectorSize=1536
AMQ_Memory__Qdrant__Distance=`"Cosine`"
AMQ_Memory__Qdrant__AutoCreateCollection=true
$(if ($config.QdrantEnableAuth -and $config.QdrantApiKey) { "AMQ_Memory__Qdrant__ApiKey=`"$($config.QdrantApiKey)`"" } else { "# AMQ_Memory__Qdrant__ApiKey=`"your-qdrant-api-key-here`"  # Optional for cloud instances" })

# ======================
# Qdrant Docker Configuration
# ======================
AMQ_QDRANT_HTTP_PORT=6333
AMQ_QDRANT_GRPC_PORT=6334
AMQ_QDRANT_WEB_UI_PORT=3000
AMQ_QDRANT_LOG_LEVEL=INFO
AMQ_QDRANT_WEB_UI_ENABLED=false
$(if ($config.QdrantEnableAuth -and $config.QdrantApiKey) { "AMQ_QDRANT_API_KEY=`"$($config.QdrantApiKey)`"" } else { "# AMQ_QDRANT_API_KEY=`"your-qdrant-api-key-here`"  # Optional for production deployments" })

# For Docker Compose - Qdrant connection
# When running with Docker Compose, use the service name as host:
# AMQ_Memory__Qdrant__Host=qdrant

# ======================
# NSFW/Waifu API Configuration
# ======================
$(if ($config.WaifuApiToken) { "AMQ_WaifuApi__Token=`"$($config.WaifuApiToken)`"" } else { "# AMQ_WaifuApi__Token=`"your-waifu-api-token-here`"" })
AMQ_WaifuApi__BaseUrl=`"https://api.waifu.im`"
AMQ_WaifuApi__Version=`"v5`"
AMQ_WaifuApi__Enabled=true

# ======================
# Web Search Configuration
# ======================
AMQ_WebSearch__Enabled=$($config.WebSearchEnabled.ToString().ToLower())
AMQ_WebSearch__Provider=`"$($config.WebSearchProvider)`"
$(if ($config.WebSearchApiKey) { "AMQ_WebSearch__ApiKey=`"$($config.WebSearchApiKey)`"" } else { "# AMQ_WebSearch__ApiKey=`"your-api-key-here`" # Required for Google/Bing" })
$(if ($config.WebSearchEngineId) { "AMQ_WebSearch__SearchEngineId=`"$($config.WebSearchEngineId)`"" } else { "# AMQ_WebSearch__SearchEngineId=`"your-search-engine-id`" # Required for Google" })
AMQ_WebSearch__MaxResults=5
AMQ_WebSearch__TimeoutSeconds=10
AMQ_WebSearch__EnableCaching=true
AMQ_WebSearch__CacheExpirationMinutes=30

# ======================
# Logging Configuration
# ======================
AMQ_Serilog__MinimumLevel__Default=`"Information`"
AMQ_Serilog__MinimumLevel__Override__System=`"Warning`"
AMQ_Serilog__MinimumLevel__Override__Microsoft=`"Warning`"
AMQ_Serilog__MinimumLevel__Override__Discord=`"Information`"

# ======================
# Optional Providers (Disabled by default)
# ======================
# Grok Provider Configuration
# AMQ_LLM__Providers__Grok__Enabled=false
# AMQ_LLM__Providers__Grok__ApiKey=`"xai-your-grok-api-key-here`"
# AMQ_LLM__Providers__Grok__BaseUrl=`"https://api.x.ai/v1/`"
# AMQ_LLM__Providers__Grok__DefaultModel=`"grok-3`"

# Grok Model Configurations
# AMQ_LLM__Providers__Grok__Models__grok-3__Name=`"Grok 3 Latest Stable`"
# AMQ_LLM__Providers__Grok__Models__grok-3__MaxTokens=131072
# AMQ_LLM__Providers__Grok__Models__grok-3__MaxOutputTokens=8192

# AMQ_LLM__Providers__Grok__Models__grok-3-mini__Name=`"Grok 3 Mini`"
# AMQ_LLM__Providers__Grok__Models__grok-3-mini__MaxTokens=131072
# AMQ_LLM__Providers__Grok__Models__grok-3-mini__MaxOutputTokens=4096

# AMQ_LLM__Providers__Grok__Models__grok-4-0709__Name=`"Grok 4 Advanced Reasoning`"
# AMQ_LLM__Providers__Grok__Models__grok-4-0709__MaxTokens=131072
# AMQ_LLM__Providers__Grok__Models__grok-4-0709__MaxOutputTokens=8192

# Gemini Provider Configuration
# AMQ_LLM__Providers__Gemini__Enabled=false
# AMQ_LLM__Providers__Gemini__ApiKey=`"your-gemini-api-key-here`"
# AMQ_LLM__Providers__Gemini__BaseUrl=`"https://generativelanguage.googleapis.com/`"
# AMQ_LLM__Providers__Gemini__DefaultModel=`"gemini-1.5-flash`"
# AMQ_LLM__Providers__Gemini__SafetyThreshold=`"BLOCK_NONE`"

# Gemini Model Configurations
# AMQ_LLM__Providers__Gemini__Models__gemini-1.5-pro__Name=`"Gemini 1.5 Pro`"
# AMQ_LLM__Providers__Gemini__Models__gemini-1.5-pro__MaxTokens=2097152
# AMQ_LLM__Providers__Gemini__Models__gemini-1.5-pro__MaxOutputTokens=8192

# AMQ_LLM__Providers__Gemini__Models__gemini-1.5-flash__Name=`"Gemini 1.5 Flash`"
# AMQ_LLM__Providers__Gemini__Models__gemini-1.5-flash__MaxTokens=1048576
# AMQ_LLM__Providers__Gemini__Models__gemini-1.5-flash__MaxOutputTokens=8192
"@

$envPath = Join-Path $PSScriptRoot ".." ".env"
Set-Content -Path $envPath -Value $envContent -Encoding UTF8
Write-Host "Created .env file" -ForegroundColor Green

# Create appsettings.json in the proper location
Write-Host "Creating appsettings.json..." -ForegroundColor Cyan

$configDir = Join-Path $PSScriptRoot ".." "source" "Amiquin.Bot"
if (!(Test-Path $configDir)) {
    Write-Host "Warning: Amiquin.Bot directory not found at expected location: $configDir" -ForegroundColor Yellow
    $configDir = Join-Path $PSScriptRoot ".." "source" "Amiquin.Bot"
    Ensure-Directory $configDir
}

$appSettingsPath = Join-Path $configDir "appsettings.json"
$examplePath = Join-Path $configDir "appsettings.example.json"

# Create streamlined appsettings.json that relies on environment variables
$appSettings = @{
    # Basic structure - most values come from environment variables
    Bot               = @{
        Token             = "your-discord-bot-token-here"
        Name              = $config.BotName
        PrintLogo         = $false
        MessageFetchCount = 40
    }
    LLM               = @{
        DefaultProvider     = $config.LLMProvider
        EnableFallback      = $true
        GlobalSystemMessage = $config.SystemMessage
        GlobalTemperature   = 0.6
        GlobalTimeout       = 120
        Providers           = @{
            OpenAI = @{
                Enabled      = $true
                ApiKey       = if ($config.OpenAIApiKey) { $config.OpenAIApiKey } else { "your-openai-api-key-here" }
                BaseUrl      = "https://api.openai.com/v1/"
                DefaultModel = $config.DefaultModel
            }
        }
    }
    Database          = @{
        Mode = $config.DatabaseMode
    }
    ConnectionStrings = @{
        AmiquinContext   = $config.DatabaseConnection
        "Amiquin-Sqlite" = if ($config.DatabaseMode -eq 1) { "Data Source=Data/Database/amiquin.db" } else { "Data Source=Data/Database/amiquin.db" }
        "Amiquin-Mysql"  = if ($config.DatabaseMode -eq 0) { $config.DatabaseConnection } else { "Server=localhost;Database=amiquin;User=amiquin;Password=your_password;Pooling=True;" }
    }
    DataPaths         = @{
        Logs          = $config.LogsPath
        Messages      = $config.MessagesPath
        Sessions      = $config.SessionsPath
        Plugins       = $config.PluginsPath
        Configuration = $config.ConfigurationPath
    }
    Memory            = @{
        Enabled               = $true
        MaxMemoriesPerSession = 1000
        MaxContextMemories    = 10
        SimilarityThreshold   = 0.7
        MinImportanceScore    = 0.3
        MinMessagesForMemory  = 3
        AutoCleanup           = $true
        CleanupOlderThanDays  = 30
        EmbeddingModel        = "text-embedding-3-small"
        MemoryTypeImportance  = @{
            summary    = 0.8
            fact       = 0.9
            preference = 0.7
            context    = 0.6
            emotion    = 0.5
            event      = 0.7
        }
        Qdrant                = @{
            Host                 = "localhost"
            Port                 = 6334
            UseHttps             = $false
            ApiKey               = $null
            CollectionName       = "amiquin_memories"
            VectorSize           = 1536
            Distance             = "Cosine"
            AutoCreateCollection = $true
        }
    }
    Voice             = @{
        TTSModelName = $config.TTSModelName
        PiperCommand = "/usr/local/bin/piper"
        Enabled      = $config.VoiceEnabled
    }
    WaifuApi          = @{
        Token   = if ($config.WaifuApiToken) { $config.WaifuApiToken } else { "your-waifu-api-token-here" }
        BaseUrl = "https://api.waifu.im"
        Version = "v5"
        Enabled = $true
    }
    Serilog           = @{
        MinimumLevel = @{
            Default  = "Information"
            Override = @{
                System    = "Warning"
                Microsoft = "Warning"
                Discord   = "Information"
            }
        }
        WriteTo      = @(
            @{ Name = "Console" }
            @{
                Name = "File"
                Args = @{
                    path                   = "$($config.LogsPath)/amiquin-.log"
                    rollingInterval        = "Day"
                    retainedFileCountLimit = 7
                    outputTemplate         = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
                }
            }
        )
        Enrich       = @("FromLogContext", "WithThreadId", "WithEnvironmentName")
        Properties   = @{
            Application = "Amiquin"
        }
    }
}

try {
    $appSettingsJson = $appSettings | ConvertTo-Json -Depth 10
    Set-Content -Path $appSettingsPath -Value $appSettingsJson -Encoding UTF8
    Write-Host "Created appsettings.json" -ForegroundColor Green
    
    # Also create example file
    $exampleSettings = $appSettings.Clone()
    $exampleSettings.Bot.Token = "your-discord-bot-token-here"
    $exampleSettings.LLM.Providers.OpenAI.ApiKey = "your-openai-api-key-here"
    $exampleSettings.WaifuApi.Token = "your-waifu-api-token-here"
    
    $exampleJson = $exampleSettings | ConvertTo-Json -Depth 10
    Set-Content -Path $examplePath -Value $exampleJson -Encoding UTF8
    Write-Host "Created appsettings.example.json" -ForegroundColor Green
}
catch {
    Write-Host "Error creating configuration files: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please create appsettings.json manually using appsettings.example.json as a template" -ForegroundColor Yellow
}

# Create data directories
Write-Host ""
Write-Host "Creating data directories..." -ForegroundColor Cyan

$dataDir = Join-Path $PSScriptRoot ".." "Data"
Ensure-Directory $dataDir
Ensure-Directory (Join-Path $dataDir "Logs")
Ensure-Directory (Join-Path $dataDir "Database")
Ensure-Directory (Join-Path $dataDir "Messages")
Ensure-Directory (Join-Path $dataDir "Sessions")
Ensure-Directory (Join-Path $dataDir "Plugins")

# Check if solution exists and build
$solutionPath = Join-Path $PSScriptRoot ".." "source" "source.sln"
if (Test-Path $solutionPath) {
    Write-Host ""
    Write-Host "Building solution..." -ForegroundColor Cyan
    Push-Location (Join-Path $PSScriptRoot ".." "source")
    
    # Restore dependencies
    Write-Host "Restoring NuGet packages..." -ForegroundColor Cyan
    dotnet restore source.sln
    
    # Build solution
    dotnet build source.sln --configuration Release
    
    Pop-Location
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Solution built successfully" -ForegroundColor Green
    }
    else {
        Write-Host "Build failed. Please check the errors above." -ForegroundColor Red
    }
}
else {
    Write-Host "Solution file not found at expected location: $solutionPath" -ForegroundColor Yellow
}

# Summary
Write-Host ""
Write-Host "=== Setup Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration files created:" -ForegroundColor Cyan
Write-Host "  - .env (Environment variables)"
Write-Host "  - source/Amiquin.Bot/Configuration/appsettings.json (Application configuration)"
Write-Host "  - source/Amiquin.Bot/Configuration/appsettings.example.json (Template for other developers)"
Write-Host ""
Write-Host "Data directories created:" -ForegroundColor Cyan
Write-Host "  - Data/Logs (Application logs)"
Write-Host "  - Data/Database (SQLite database)"
Write-Host "  - Data/Messages (Message storage)"
Write-Host "  - Data/Sessions (Session storage)"
Write-Host "  - Data/Plugins (Plugin storage)"
Write-Host ""

# Show warnings for missing configuration
$hasWarnings = $false

if (-not $config.BotToken) {
    if (-not $hasWarnings) {
        Write-Host "IMPORTANT: Missing configuration" -ForegroundColor Yellow
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        $hasWarnings = $true
    }
    Write-Host "  • Discord Bot Token: Required for bot functionality" -ForegroundColor Yellow
    Write-Host "    - Update 'AMQ_Discord__Token' in .env file"
}

if (-not $config.OpenAIApiKey) {
    if (-not $hasWarnings) {
        Write-Host "IMPORTANT: Missing configuration" -ForegroundColor Yellow
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        $hasWarnings = $true
    }
    Write-Host "  • OpenAI API Key: Required for AI chat features" -ForegroundColor Yellow
    Write-Host "    - Update 'AMQ_LLM__Providers__OpenAI__ApiKey' in .env file"
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow

$stepNumber = 1

if (-not $config.BotToken) {
    Write-Host "$stepNumber. Add your Discord Bot Token to .env file"
    Write-Host "   - Get token from: https://discord.com/developers/applications"
    $stepNumber++
}

if (-not $config.OpenAIApiKey) {
    Write-Host "$stepNumber. Add your OpenAI API key to .env file (for AI features)"
    Write-Host "   - Get key from: https://platform.openai.com/api-keys"
    $stepNumber++
}

Write-Host "$stepNumber. Start Qdrant vector database (required for memory features):"
Write-Host "   docker-compose --profile vector up -d"
Write-Host "   # Or standalone: docker-compose --profile qdrant-only up -d"
$stepNumber++

if (Test-Path $solutionPath) {
    Write-Host "$stepNumber. Run database migrations (automatic on startup, or manually):"
    Write-Host "   cd source && dotnet ef database update -p Amiquin.Infrastructure -s Amiquin.Bot"
    $stepNumber++
    
    Write-Host "$stepNumber. Start the application:"
    Write-Host "   cd source/Amiquin.Bot && dotnet run"
    Write-Host "   # Or with Docker: docker-compose --profile full up -d"
}
else {
    Write-Host "$stepNumber. Check that the solution exists at: source/source.sln"
    Write-Host "   Current directory: $PSScriptRoot"
}

Write-Host ""
Write-Host "All configuration values can be overridden using environment variables with AMQ_ prefix." -ForegroundColor Gray
Write-Host "For more information, see the documentation at dev/docs/" -ForegroundColor Gray
Write-Host ""