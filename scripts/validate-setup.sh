#!/bin/bash

# Validation script for Amiquin project setup and configuration
# Comprehensive validation script that checks:
# - Configuration file completeness
# - External dependencies
# - Environment setup
# - Service connectivity
# - Security configuration

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Default values
PRODUCTION=false
VERBOSE=false
FIX=false

# Counters
SUCCESS_COUNT=0
WARNING_COUNT=0
ERROR_COUNT=0

# Script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Parse command line arguments
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
        --help|-h)
            echo -e "${CYAN}Amiquin Setup Validation Script${NC}"
            echo ""
            echo "This script validates your Amiquin project setup by checking:"
            echo "  - Configuration file completeness and validity"
            echo "  - External service dependencies (Docker, ffmpeg, etc.)"
            echo "  - Environment variable configuration"
            echo "  - Service connectivity (Qdrant, MySQL, APIs)"
            echo "  - Security configuration for production"
            echo ""
            echo "Usage:"
            echo "  $0                    # Basic validation"
            echo "  $0 --production       # Production-level validation"
            echo "  $0 --verbose          # Detailed output"
            echo "  $0 --fix              # Attempt to fix issues automatically"
            echo ""
            echo "Parameters:"
            echo "  --production   Perform production-level security and performance checks"
            echo "  --verbose      Show detailed information about each check"
            echo "  --fix          Attempt to automatically fix detected issues"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Navigate to project root
cd "$PROJECT_ROOT"

echo -e "${CYAN}=== Amiquin Setup Validation ===${NC}"
if [ "$PRODUCTION" = true ]; then
    echo -e "${YELLOW}Production Mode: Enhanced security and performance checks enabled${NC}"
fi
echo ""

# Validation functions
test_check_result() {
    local check_name="$1"
    local success="$2"
    local message="$3"
    local fix_command="$4"
    
    if [ "$success" = true ]; then
        echo -e "${GREEN}✓ $check_name${NC}"
        ((SUCCESS_COUNT++))
        if [ "$VERBOSE" = true ] && [ -n "$message" ]; then
            echo -e "${GRAY}  $message${NC}"
        fi
    else
        if [[ "$message" =~ WARNING|WARN ]]; then
            echo -e "${YELLOW}⚠ $check_name${NC}"
            ((WARNING_COUNT++))
        else
            echo -e "${RED}❌ $check_name${NC}"
            ((ERROR_COUNT++))
        fi
        
        if [ -n "$message" ]; then
            echo -e "${GRAY}  $message${NC}"
        fi
        
        if [ "$FIX" = true ] && [ -n "$fix_command" ]; then
            echo -e "${CYAN}  Attempting fix: $fix_command${NC}"
            if eval "$fix_command"; then
                echo -e "${GREEN}  ✓ Fix applied${NC}"
            else
                echo -e "${RED}  ❌ Fix failed${NC}"
            fi
        fi
    fi
}

test_file_exists() {
    local path="$1"
    local description="$2"
    
    if [ -f "$path" ] || [ -d "$path" ]; then
        test_check_result "$description exists" true "Path: $path"
        echo true
    else
        test_check_result "$description exists" false "Path: $path"
        echo false
    fi
}

test_environment_file() {
    echo -e "${CYAN}Checking environment configuration...${NC}"
    
    # Check .env file exists
    local env_exists=$(test_file_exists ".env" ".env file")
    
    if [ "$env_exists" = true ]; then
        # Required variables
        local required_vars=(
            "AMQ_Discord__Token:true"
            "AMQ_LLM__Providers__OpenAI__ApiKey:true"
            "AMQ_Database__Mode:true"
            "AMQ_Memory__Enabled:false"
            "AMQ_Memory__Qdrant__Host:false"
        )
        
        for var_info in "${required_vars[@]}"; do
            IFS=':' read -r var_name required <<< "$var_info"
            
            if grep -q "^$var_name=" .env && ! grep -q "your-.*-here" .env | grep -q "$var_name"; then
                local value=$(grep "^$var_name=" .env | cut -d'=' -f2- | xargs)
                if [ -n "$value" ]; then
                    test_check_result "$var_name configured" true
                else
                    test_check_result "$var_name configured" false "Empty value"
                fi
            else
                if [ "$required" = true ]; then
                    test_check_result "$var_name configured" false "Required for basic functionality"
                else
                    test_check_result "$var_name configured" false "WARNING: Optional feature not configured"
                fi
            fi
        done
    fi
    
    # Check appsettings.json
    test_file_exists "source/Amiquin.Bot/Data/Configuration/appsettings.json" "appsettings.json"
    
    echo ""
}

test_external_dependencies() {
    echo -e "${CYAN}Checking external dependencies...${NC}"
    
    # Docker
    if command -v docker > /dev/null 2>&1; then
        test_check_result "Docker installed" true
        
        if docker info > /dev/null 2>&1; then
            test_check_result "Docker running" true
        else
            test_check_result "Docker running" false "Docker daemon is not running"
        fi
    else
        test_check_result "Docker installed" false "Docker is required for Qdrant and MySQL services"
    fi
    
    # Docker Compose
    if command -v docker-compose > /dev/null 2>&1; then
        test_check_result "Docker Compose installed" true
    else
        test_check_result "Docker Compose installed" false "Required for multi-service deployment"
    fi
    
    # .NET SDK
    if command -v dotnet > /dev/null 2>&1; then
        local dotnet_version=$(dotnet --version 2>/dev/null)
        if [[ "$dotnet_version" =~ ^9\. ]]; then
            test_check_result ".NET 9.0 SDK installed" true "Version: $dotnet_version"
        else
            test_check_result ".NET 9.0 SDK installed" false "Version: $dotnet_version (requires 9.x)"
        fi
    else
        test_check_result ".NET 9.0 SDK installed" false "Required to build and run Amiquin"
    fi
    
    # FFmpeg (optional but recommended)
    if command -v ffmpeg > /dev/null 2>&1; then
        test_check_result "FFmpeg installed" true "Required for voice features"
    else
        test_check_result "FFmpeg installed" false "WARNING: Voice features will not work without FFmpeg"
    fi
    
    echo ""
}

test_service_connectivity() {
    echo -e "${CYAN}Checking service connectivity...${NC}"
    
    # Test Qdrant connection
    if curl -f http://localhost:6333/health > /dev/null 2>&1; then
        test_check_result "Qdrant service reachable" true
    else
        test_check_result "Qdrant service reachable" false "Start Qdrant with: docker-compose --profile qdrant-only up -d"
    fi
    
    # Test MySQL connection (if configured)
    if grep -q "^AMQ_Database__Mode=0" .env 2>/dev/null; then
        if docker exec mysql-amiquin-instance mysqladmin ping -h localhost > /dev/null 2>&1; then
            test_check_result "MySQL service reachable" true
        else
            test_check_result "MySQL service reachable" false "Start MySQL with: docker-compose --profile database up -d"
        fi
    else
        test_check_result "SQLite database mode" true "No external database connection required"
    fi
    
    echo ""
}

test_security_configuration() {
    if [ "$PRODUCTION" != true ]; then
        return
    fi
    
    echo -e "${CYAN}Checking security configuration (Production mode)...${NC}"
    
    # Check for placeholder tokens
    local placeholder_count=$(grep -c "your-.*-here\|sk-test\|placeholder" .env 2>/dev/null || echo 0)
    test_check_result "No placeholder tokens in production" $([ "$placeholder_count" -eq 0 ] && echo true || echo false) "Found $placeholder_count placeholder tokens"
    
    # Check for Qdrant authentication in production
    if grep -q "^AMQ_Memory__Qdrant__ApiKey=" .env && ! grep -q "your-.*-here" .env | grep -q "AMQ_Memory__Qdrant__ApiKey"; then
        test_check_result "Qdrant authentication configured" true
    else
        test_check_result "Qdrant authentication configured" false "Recommended for production deployments"
    fi
    
    # Check for strong passwords
    local mysql_passwords=$(grep "^AMQ_DB_.*_PASSWORD=" .env 2>/dev/null || echo "")
    if [ -n "$mysql_passwords" ]; then
        while IFS= read -r password_line; do
            local var_name=$(echo "$password_line" | cut -d'=' -f1)
            local value=$(echo "$password_line" | cut -d'=' -f2- | tr -d '"')
            
            # Check password strength: 16+ chars, mixed case, numbers
            if [[ ${#value} -ge 16 && "$value" =~ [A-Z] && "$value" =~ [a-z] && "$value" =~ [0-9] ]]; then
                test_check_result "$var_name uses strong password" true
            else
                test_check_result "$var_name uses strong password" false "Should be 16+ chars with mixed case and numbers"
            fi
        done <<< "$mysql_passwords"
    fi
    
    echo ""
}

test_project_structure() {
    echo -e "${CYAN}Checking project structure...${NC}"
    
    # Essential directories
    local required_dirs=(
        "source"
        "source/Amiquin.Bot"
        "source/Amiquin.Core"
        "source/Amiquin.Infrastructure"
        "Data"
        "Data/Logs"
        "Data/Database"
        "scripts"
    )
    
    for dir in "${required_dirs[@]}"; do
        test_file_exists "$dir" "Directory $dir"
    done
    
    # Solution file
    test_file_exists "source/source.sln" "Solution file"
    
    # Docker files
    test_file_exists "docker-compose.yml" "Docker Compose configuration"
    test_file_exists "source/Amiquin.Bot/dockerfile" "Dockerfile"
    
    echo ""
}

test_build_status() {
    echo -e "${CYAN}Checking build status...${NC}"
    
    if [ -f "source/source.sln" ]; then
        cd "source"
        
        if [ "$VERBOSE" = true ]; then
            echo -e "${GRAY}  Restoring packages...${NC}"
        fi
        
        if dotnet restore source.sln --verbosity quiet > /dev/null 2>&1; then
            test_check_result "NuGet package restore" true
        else
            test_check_result "NuGet package restore" false
        fi
        
        if [ "$VERBOSE" = true ]; then
            echo -e "${GRAY}  Building solution...${NC}"
        fi
        
        if dotnet build source.sln --configuration Release --no-restore --verbosity quiet > /dev/null 2>&1; then
            test_check_result "Solution builds successfully" true
        else
            test_check_result "Solution builds successfully" false
        fi
        
        cd "$PROJECT_ROOT"
    else
        test_check_result "Solution file exists" false "Cannot test build without solution file"
    fi
    
    echo ""
}

# Run all validations
test_project_structure
test_environment_file
test_external_dependencies
test_service_connectivity
test_security_configuration
test_build_status

# Summary
echo -e "${CYAN}=== Validation Summary ===${NC}"
echo -e "${GREEN}✓ Passed: $SUCCESS_COUNT${NC}"
if [ $WARNING_COUNT -gt 0 ]; then
    echo -e "${YELLOW}⚠ Warnings: $WARNING_COUNT${NC}"
fi
if [ $ERROR_COUNT -gt 0 ]; then
    echo -e "${RED}❌ Errors: $ERROR_COUNT${NC}"
fi

echo ""

if [ $ERROR_COUNT -eq 0 ]; then
    echo -e "${GREEN}🎉 Setup validation completed successfully!${NC}"
    if [ $WARNING_COUNT -gt 0 ]; then
        echo -e "${YELLOW}Note: Some warnings were found but they don't prevent basic functionality.${NC}"
    fi
else
    echo -e "${RED}❌ Setup validation failed with $ERROR_COUNT errors.${NC}"
    echo -e "${YELLOW}Please address the errors above before running Amiquin.${NC}"
    
    if [ "$FIX" != true ]; then
        echo -e "${CYAN}Tip: Run with --fix to attempt automatic fixes where possible.${NC}"
    fi
fi

echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "• Fix any errors or warnings shown above"
echo "• Start required services: ./scripts/start-qdrant.sh"
echo "• Run the application: cd source/Amiquin.Bot && dotnet run"
echo "• For production: ./scripts/deploy-production.sh"

exit $ERROR_COUNT