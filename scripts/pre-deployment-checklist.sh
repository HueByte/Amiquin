#!/bin/bash

# Pre-deployment checklist for Amiquin bot
# Validates configuration, dependencies, and readiness for deployment

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m'

# Script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

# Counters
PASS_COUNT=0
FAIL_COUNT=0
WARN_COUNT=0

# Flags
PRODUCTION=false
VERBOSE=false
FIX=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --production)
            PRODUCTION=true
            shift
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        --fix)
            FIX=true
            shift
            ;;
        --help)
            echo -e "${CYAN}Amiquin Pre-Deployment Checklist${NC}"
            echo ""
            echo "This script validates your Amiquin project before deployment:"
            echo "  ✓ Configuration completeness"
            echo "  ✓ Environment variables"
            echo "  ✓ Dependencies and services"
            echo "  ✓ Build and compilation"
            echo "  ✓ Security settings"
            echo "  ✓ Docker readiness"
            echo ""
            echo "Usage:"
            echo "  ./pre-deployment-checklist.sh               # Basic checks"
            echo "  ./pre-deployment-checklist.sh --production  # Production checks"
            echo "  ./pre-deployment-checklist.sh --fix         # Auto-fix issues"
            echo "  ./pre-deployment-checklist.sh --verbose     # Detailed output"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo -e "${CYAN}=== Amiquin Pre-Deployment Checklist ===${NC}"
if [ "$PRODUCTION" = true ]; then
    echo -e "Mode: ${YELLOW}Production (enhanced security checks)${NC}"
else
    echo -e "Mode: ${GREEN}Standard${NC}"
fi
echo ""

# Test function
test_item() {
    local name="$1"
    local pass="$2"
    local message="${3:-}"
    local level="${4:-Error}"
    
    if [ "$pass" = true ]; then
        echo -e "${GREEN}✓ $name${NC}"
        ((PASS_COUNT++))
        if [ "$VERBOSE" = true ] && [ ! -z "$message" ]; then
            echo -e "  ${GRAY}ℹ $message${NC}"
        fi
    else
        if [ "$level" = "Warning" ]; then
            echo -e "${YELLOW}⚠ $name${NC}"
            ((WARN_COUNT++))
        else
            echo -e "${RED}❌ $name${NC}"
            ((FAIL_COUNT++))
        fi
        if [ ! -z "$message" ]; then
            echo -e "  ${GRAY}→ $message${NC}"
        fi
    fi
}

# ====================
# 1. Configuration Files
# ====================
echo -e "\n${CYAN}[1/8] Configuration Files${NC}"

test_item ".env file exists" "$([ -f .env ] && echo true || echo false)" "Run ./setup-project.sh to create configuration"
test_item "appsettings.json exists" "$([ -f source/Amiquin.Bot/appsettings.json ] && echo true || echo false)" "Copy from appsettings.example.json"
test_item "docker-compose.yml exists" "$([ -f docker-compose.yml ] && echo true || echo false)" "Required for Docker deployment"

if [ -f .env ]; then
    test_item "Discord token configured" "$(grep -q 'AMQ_Discord__Token=".\+"' .env && echo true || echo false)" "Add Discord bot token to .env"
    test_item "OpenAI API key configured" "$(grep -q 'AMQ_LLM__Providers__OpenAI__ApiKey="sk-.\+"' .env && echo true || echo false)" "Add OpenAI API key to .env"
    
    if [ "$PRODUCTION" = true ]; then
        test_item "MySQL passwords set" "$(grep -q 'AMQ_DB_ROOT_PASSWORD=".\{16,\}"' .env && grep -q 'AMQ_DB_USER_PASSWORD=".\{16,\}"' .env && echo true || echo false)" "Passwords should be 16+ characters" "Warning"
    fi
fi

# ====================
# 2. Dependencies
# ====================
echo -e "\n${CYAN}[2/8] Dependencies & Tools${NC}"

DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "")
test_item ".NET SDK installed" "$([ ! -z "$DOTNET_VERSION" ] && echo true || echo false)" "Install .NET SDK 9.0+. Found: $DOTNET_VERSION"

DOCKER_RUNNING=$(docker info &>/dev/null && echo true || echo false)
test_item "Docker running" "$DOCKER_RUNNING" "Start Docker" "Warning"

DOCKER_COMPOSE=$(docker-compose --version &>/dev/null && echo true || echo false)
test_item "Docker Compose available" "$DOCKER_COMPOSE" "Install Docker Compose" "Warning"

FFMPEG=$(ffmpeg -version &>/dev/null && echo true || echo false)
test_item "ffmpeg available" "$FFMPEG" "Required for voice features" "Warning"

# ====================
# 3. Project Structure
# ====================
echo -e "\n${CYAN}[3/8] Project Structure${NC}"

test_item "Solution file exists" "$([ -f source/source.sln ] && echo true || echo false)"
test_item "Bot project exists" "$([ -f source/Amiquin.Bot/Amiquin.Bot.csproj ] && echo true || echo false)"
test_item "Core project exists" "$([ -f source/Amiquin.Core/Amiquin.Core.csproj ] && echo true || echo false)"
test_item "Infrastructure project exists" "$([ -f source/Amiquin.Infrastructure/Amiquin.Infrastructure.csproj ] && echo true || echo false)"

DATA_DIRS=("Data/Database" "Data/Logs" "Data/Messages" "Data/Sessions" "Data/Plugins")
ALL_DIRS_EXIST=true
for dir in "${DATA_DIRS[@]}"; do
    if [ ! -d "$dir" ]; then
        if [ "$FIX" = true ]; then
            mkdir -p "$dir"
            echo -e "  ${GREEN}✓ Created $dir${NC}"
        else
            ALL_DIRS_EXIST=false
        fi
    fi
done
test_item "Data directories exist" "$ALL_DIRS_EXIST"

# ====================
# 4. Build Status
# ====================
echo -e "\n${CYAN}[4/8] Build Status${NC}"

echo -n "  Building solution..."
if dotnet build source/source.sln -c Release --nologo -v quiet &>/dev/null; then
    echo -e " ${GREEN}Success${NC}"
    ((PASS_COUNT++))
else
    echo -e " ${RED}Failed${NC}"
    ((FAIL_COUNT++))
fi

test_item "Bot DLL exists" "$([ -f source/Amiquin.Bot/bin/Release/net10.0/Amiquin.Bot.dll ] && echo true || echo false)"

# ====================
# 5. Database Configuration
# ====================
echo -e "\n${CYAN}[5/8] Database Configuration${NC}"

if [ -f .env ]; then
    DB_MODE=$(grep 'AMQ_Database__Mode=' .env | cut -d= -f2 || echo "1")
    
    if [ "$DB_MODE" = "0" ]; then
        echo -e "  ${GRAY}Database mode: MySQL${NC}"
        test_item "MySQL connection string set" "$(grep -q 'AMQ_ConnectionStrings__Amiquin-Mysql=' .env && echo true || echo false)" "Configure MySQL connection"
        
        if [ "$DOCKER_RUNNING" = true ]; then
            MYSQL_CONTAINER=$(docker ps --filter "name=mysql" --format "{{.Names}}" 2>/dev/null || echo "")
            test_item "MySQL container running" "$([ ! -z "$MYSQL_CONTAINER" ] && echo true || echo false)" "Start MySQL: docker-compose --profile database up -d" "Warning"
        fi
    else
        echo -e "  ${GRAY}Database mode: SQLite${NC}"
        test_item "SQLite directory exists" "$([ -d Data/Database ] && echo true || echo false)"
    fi
fi

# ====================
# 6. Memory System (Qdrant)
# ====================
echo -e "\n${CYAN}[6/8] Memory System (Qdrant)${NC}"

if [ -f .env ]; then
    MEMORY_ENABLED=$(grep -q 'AMQ_Memory__Enabled=true' .env && echo true || echo false)
    
    if [ "$MEMORY_ENABLED" = true ]; then
        echo -e "  ${GRAY}Memory system: Enabled${NC}"
        test_item "Qdrant host configured" "$(grep -q 'AMQ_Memory__Qdrant__Host=' .env && echo true || echo false)" "Set Qdrant host"
        test_item "Qdrant port configured" "$(grep -q 'AMQ_Memory__Qdrant__Port=' .env && echo true || echo false)" "Set Qdrant port"
        
        if [ "$DOCKER_RUNNING" = true ]; then
            QDRANT_CONTAINER=$(docker ps --filter "name=qdrant" --format "{{.Names}}" 2>/dev/null || echo "")
            test_item "Qdrant container running" "$([ ! -z "$QDRANT_CONTAINER" ] && echo true || echo false)" "Start Qdrant: docker-compose --profile qdrant-only up -d" "Warning"
        fi
    else
        echo -e "  ${GRAY}Memory system: Disabled${NC}"
        test_item "Memory configuration" true "Memory system is optional" "Warning"
    fi
fi

# ====================
# 7. Web Search Configuration
# ====================
echo -e "\n${CYAN}[7/8] Web Search Configuration${NC}"

if [ -f .env ]; then
    WEB_SEARCH_ENABLED=$(grep -q 'AMQ_WebSearch__Enabled=true' .env && echo true || echo false)
    
    if [ "$WEB_SEARCH_ENABLED" = true ]; then
        echo -e "  ${GRAY}Web search: Enabled${NC}"
        PROVIDER=$(grep 'AMQ_WebSearch__Provider=' .env | cut -d'"' -f2 || echo "Unknown")
        echo -e "  ${GRAY}Provider: $PROVIDER${NC}"
        
        if [ "$PROVIDER" = "Google" ]; then
            test_item "Google API key set" "$(grep -q 'AMQ_WebSearch__ApiKey=".\+"' .env && echo true || echo false)" "Configure Google Custom Search API key"
            test_item "Google Search Engine ID set" "$(grep -q 'AMQ_WebSearch__SearchEngineId=".\+"' .env && echo true || echo false)" "Configure Google Search Engine ID"
        elif [ "$PROVIDER" = "Bing" ]; then
            test_item "Bing API key set" "$(grep -q 'AMQ_WebSearch__ApiKey=".\+"' .env && echo true || echo false)" "Configure Bing Search API key"
        else
            test_item "DuckDuckGo provider" true "No API key required"
        fi
    else
        echo -e "  ${GRAY}Web search: Disabled${NC}"
        test_item "Web search configuration" true "Web search is optional" "Warning"
    fi
fi

# ====================
# 8. Security Checks
# ====================
if [ "$PRODUCTION" = true ]; then
    echo -e "\n${CYAN}[8/8] Security Checks (Production)${NC}"
    
    if [ -f .env ]; then
        test_item "No default passwords" "$(! grep -qE 'password|changeme|admin|root123' .env && echo true || echo false)" "Use strong, unique passwords"
        test_item "Logging level appropriate" "$(grep -qE 'AMQ_Serilog__MinimumLevel__Default="(Warning|Error|Information)"' .env && echo true || echo false)" "Set appropriate log level"
    fi
    
    test_item ".env not in git" "$(git check-ignore .env &>/dev/null && echo true || echo false)" ".env should be in .gitignore"
    test_item ".gitignore exists" "$([ -f .gitignore ] && echo true || echo false)"
else
    echo -e "\n${CYAN}[8/8] Security Checks${NC}"
    echo -e "  ${YELLOW}→ Run with --production flag for enhanced security checks${NC}"
fi

# ====================
# Summary
# ====================
echo -e "\n${CYAN}==================== Summary ====================${NC}"
echo -e "${GREEN}✓ Passed:  $PASS_COUNT${NC}"
if [ $WARN_COUNT -gt 0 ]; then
    echo -e "${YELLOW}⚠ Warnings: $WARN_COUNT${NC}"
fi
if [ $FAIL_COUNT -gt 0 ]; then
    echo -e "${RED}❌ Failed:  $FAIL_COUNT${NC}"
fi
echo -e "${CYAN}=================================================${NC}"

if [ $FAIL_COUNT -eq 0 ] && [ $WARN_COUNT -eq 0 ]; then
    echo -e "\n${GREEN}🚀 All checks passed! Ready for deployment.${NC}"
    exit 0
elif [ $FAIL_COUNT -eq 0 ]; then
    echo -e "\n${YELLOW}✅ No critical issues found. Review warnings before deployment.${NC}"
    exit 0
else
    echo -e "\n${RED}⚠️  Critical issues found. Fix errors before deploying.${NC}"
    echo -e "${YELLOW}Run './setup-project.sh' to fix configuration issues.${NC}"
    exit 1
fi
