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
    [switch]$Default
)

if ($Help) {
    Write-Host "Amiquin Project Setup Script" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "This script will configure your Amiquin project by:"
    Write-Host "  - Creating configuration files from templates"
    Write-Host "  - Prompting for OpenAI API Key (optional)"
    Write-Host "  - Setting up data directories"
    Write-Host "  - Building the solution"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  ./setup-project.ps1           # Interactive mode (recommended)"
    Write-Host "  ./setup-project.ps1 -Default  # Interactive with sensible defaults"
    Write-Host "  ./setup-project.ps1 -NonInteractive  # Automated setup with defaults"
    Write-Host "  ./setup-project.ps1 -Help     # Show this help"
    exit 0
}

Write-Host "=== Amiquin Project Setup ===" -ForegroundColor Cyan
Write-Host ""

# Configuration values aligned with current AMQ_ prefix system
$config = @{
    # Bot configuration
    BotToken = ""
    BotName = "Amiquin"
    
    # LLM configuration
    LLMProvider = "OpenAI"
    OpenAIApiKey = ""
    SystemMessage = "I want you to act as personal assistant called Amiquin. You are friendly, helpful and professional."
    DefaultModel = "gpt-4o-mini"
    
    # Database
    DatabaseMode = 1  # SQLite by default
    DatabaseConnection = "Data Source=Data/Database/amiquin.db"
    
    # Data paths
    LogsPath = "Data/Logs"
    MessagesPath = "Data/Messages"
    SessionsPath = "Data/Sessions"
    PluginsPath = "Data/Plugins"
    ConfigurationPath = "Configuration"
    
    # Voice/TTS
    VoiceEnabled = $true
    TTSModelName = "en_GB-northern_english_male-medium"
}

# Generate secure defaults
function New-SecureString {
    param([int]$Length = 32)
    $chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*'
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

if ($NonInteractive) {
    Write-Host "Running in non-interactive mode with default values" -ForegroundColor Yellow
} else {
    Write-Host "This script will configure your Amiquin project with the necessary settings." -ForegroundColor Green
    Write-Host "Press Enter to use default values shown in [brackets]" -ForegroundColor Gray
    Write-Host ""
    
    # Discord Bot Configuration
    Write-Host "=== Discord Bot Configuration ===" -ForegroundColor Cyan
    $botToken = Read-Host "Enter Discord Bot Token (required) [leave empty to configure later]"
    if ($botToken) { 
        $config.BotToken = $botToken 
        Write-Host "Discord bot token configured successfully" -ForegroundColor Green
    } else {
        Write-Host "Discord bot token will need to be configured later" -ForegroundColor Yellow
    }
    
    # OpenAI Configuration
    Write-Host "\n=== AI Configuration ===" -ForegroundColor Cyan
    $openAIKey = Read-Host "Enter OpenAI API Key (required for AI features) [leave empty to configure later]"
    if ($openAIKey) { 
        $config.OpenAIApiKey = $openAIKey 
        Write-Host "OpenAI API key configured successfully" -ForegroundColor Green
    } else {
        Write-Host "OpenAI API key will need to be configured later for AI features to work" -ForegroundColor Yellow
    }
    
    # System Message
    if ($Default) {
        Write-Host "Using default system message for Amiquin" -ForegroundColor Gray
    } else {
        $systemMessage = Read-Host "Enter AI system message [$($config.SystemMessage)]"
        if ($systemMessage) { $config.SystemMessage = $systemMessage }
    }
    
    # Database configuration
    Write-Host "\n=== Database Configuration ===" -ForegroundColor Cyan
    if (!$Default) {
        Write-Host "Select Database Type:"
        Write-Host "1. SQLite (default - recommended for most users)"
        Write-Host "2. MySQL (for production/multi-instance setups)"
        $dbChoice = Read-Host "Enter choice [1]"
        
        if ($dbChoice -eq "2") {
            $config.DatabaseMode = 0
            $mysqlConnection = Read-Host "Enter MySQL connection string [Server=localhost;Database=amiquin_db;Uid=amiquin_user;Pwd=amiquin_password;]"
            if ($mysqlConnection) { $config.DatabaseConnection = $mysqlConnection }
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
}

# Create .env file
Write-Host ""
Write-Host "Creating .env file..." -ForegroundColor Cyan

$envContent = @"
# Amiquin Environment Configuration
# Generated by setup script on $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
# All configuration values use AMQ_ prefix as defined in .env.example

# ======================
# Bot Configuration
# ======================
$(if ($config.BotToken) { "AMQ_Bot__Token=$($config.BotToken)" } else { "# AMQ_Bot__Token=your-discord-bot-token-here" })
AMQ_Bot__Name=$($config.BotName)
AMQ_Bot__PrintLogo=false
AMQ_Bot__MessageFetchCount=40

# ======================
# LLM (AI Language Model) Configuration
# ======================
AMQ_LLM__DefaultProvider=$($config.LLMProvider)
AMQ_LLM__EnableFallback=true
AMQ_LLM__GlobalSystemMessage=$($config.SystemMessage)
AMQ_LLM__GlobalTemperature=0.6
AMQ_LLM__GlobalTimeout=120

# OpenAI Provider Configuration
AMQ_LLM__Providers__OpenAI__Enabled=true
$(if ($config.OpenAIApiKey) { "AMQ_LLM__Providers__OpenAI__ApiKey=$($config.OpenAIApiKey)" } else { "# AMQ_LLM__Providers__OpenAI__ApiKey=sk-your-openai-api-key-here" })
AMQ_LLM__Providers__OpenAI__BaseUrl=https://api.openai.com/v1/
AMQ_LLM__Providers__OpenAI__DefaultModel=$($config.DefaultModel)

# OpenAI Model Configurations
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__Name=GPT-4 Omni
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__MaxTokens=128000
AMQ_LLM__Providers__OpenAI__Models__gpt-4o__MaxOutputTokens=4096

AMQ_LLM__Providers__OpenAI__Models__gpt-4o-mini__Name=GPT-4 Omni Mini
AMQ_LLM__Providers__OpenAI__Models__gpt-4o-mini__MaxTokens=128000
AMQ_LLM__Providers__OpenAI__Models__gpt-4o-mini__MaxOutputTokens=16384

# ======================
# Database Configuration
# ======================
AMQ_Database__Mode=$($config.DatabaseMode)
AMQ_ConnectionStrings__AmiquinContext=$($config.DatabaseConnection)

# ======================
# Data Paths Configuration
# ======================
AMQ_DataPaths__Logs=$($config.LogsPath)
AMQ_DataPaths__Messages=$($config.MessagesPath)
AMQ_DataPaths__Sessions=$($config.SessionsPath)
AMQ_DataPaths__Plugins=$($config.PluginsPath)
AMQ_DataPaths__Configuration=$($config.ConfigurationPath)

# ======================
# Voice/TTS Configuration
# ======================
AMQ_Voice__TTSModelName=$($config.TTSModelName)
AMQ_Voice__PiperCommand=/usr/local/bin/piper
AMQ_Voice__Enabled=$($config.VoiceEnabled.ToString().ToLower())

# ======================
# Logging Configuration
# ======================
AMQ_Serilog__MinimumLevel__Default=Information
AMQ_Serilog__MinimumLevel__Override__System=Warning
AMQ_Serilog__MinimumLevel__Override__Microsoft=Warning
AMQ_Serilog__MinimumLevel__Override__Discord=Information

# ======================
# Optional Providers (Disabled by default)
# ======================
# Grok Provider Configuration
# AMQ_LLM__Providers__Grok__Enabled=false
# AMQ_LLM__Providers__Grok__ApiKey=xai-your-grok-api-key-here

# Gemini Provider Configuration
# AMQ_LLM__Providers__Gemini__Enabled=false
# AMQ_LLM__Providers__Gemini__ApiKey=your-gemini-api-key-here
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
    Bot = @{
        Token = "your-discord-bot-token-here"
        Name = $config.BotName
        PrintLogo = $false
        MessageFetchCount = 40
    }
    LLM = @{
        DefaultProvider = $config.LLMProvider
        EnableFallback = $true
        GlobalSystemMessage = $config.SystemMessage
        GlobalTemperature = 0.6
        GlobalTimeout = 120
        Providers = @{
            OpenAI = @{
                Enabled = $true
                ApiKey = if ($config.OpenAIApiKey) { $config.OpenAIApiKey } else { "your-openai-api-key-here" }
                BaseUrl = "https://api.openai.com/v1/"
                DefaultModel = $config.DefaultModel
            }
        }
    }
    Database = @{
        Mode = $config.DatabaseMode
    }
    ConnectionStrings = @{
        AmiquinContext = $config.DatabaseConnection
    }
    DataPaths = @{
        Logs = $config.LogsPath
        Messages = $config.MessagesPath
        Sessions = $config.SessionsPath
        Plugins = $config.PluginsPath
        Configuration = $config.ConfigurationPath
    }
    Voice = @{
        TTSModelName = $config.TTSModelName
        PiperCommand = "/usr/local/bin/piper"
        Enabled = $config.VoiceEnabled
    }
    Serilog = @{
        MinimumLevel = @{
            Default = "Information"
            Override = @{
                System = "Warning"
                Microsoft = "Warning"
                Discord = "Information"
            }
        }
        WriteTo = @(
            @{ Name = "Console" }
            @{
                Name = "File"
                Args = @{
                    path = "$($config.LogsPath)/amiquin-.log"
                    rollingInterval = "Day"
                    retainedFileCountLimit = 7
                    outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
                }
            }
        )
        Enrich = @("FromLogContext", "WithThreadId", "WithEnvironmentName")
        Properties = @{
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
    
    $exampleJson = $exampleSettings | ConvertTo-Json -Depth 10
    Set-Content -Path $examplePath -Value $exampleJson -Encoding UTF8
    Write-Host "Created appsettings.example.json" -ForegroundColor Green
} catch {
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
    } else {
        Write-Host "Build failed. Please check the errors above." -ForegroundColor Red
    }
} else {
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
    Write-Host "    - Update 'AMQ_Bot__Token' in .env file"
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

if (Test-Path $solutionPath) {
    Write-Host "$stepNumber. Run database migrations (automatic on startup, or manually):"
    Write-Host "   cd source && dotnet ef database update -p Amiquin.Infrastructure -s Amiquin.Bot"
    $stepNumber++
    
    Write-Host "$stepNumber. Start the application:"
    Write-Host "   cd source/Amiquin.Bot && dotnet run"
    Write-Host "   # Or with Docker: docker-compose up"
} else {
    Write-Host "$stepNumber. Check that the solution exists at: source/source.sln"
    Write-Host "   Current directory: $PSScriptRoot"
}

Write-Host ""
Write-Host "All configuration values can be overridden using environment variables with AMIQUIN_ prefix." -ForegroundColor Gray
Write-Host "For more information, see the documentation at dev/docs/" -ForegroundColor Gray
Write-Host ""