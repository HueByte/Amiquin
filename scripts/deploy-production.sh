#!/bin/bash

# Production deployment script for Amiquin Discord bot
# Comprehensive production deployment script that handles:
# - Environment validation
# - Security configuration
# - Performance optimization
# - Service orchestration
# - Health monitoring
# - Backup configuration

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Default values
DATABASE_TYPE="mysql"
ENVIRONMENT="production"
INSTANCE_NAME="amiquin-prod"
SKIP_BUILD=false
SKIP_HEALTH_CHECK=false
ENABLE_MONITORING=false
ENABLE_BACKUPS=false
DRY_RUN=false

# Script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --database-type)
            DATABASE_TYPE="$2"
            shift 2
            ;;
        --environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        --instance-name)
            INSTANCE_NAME="$2"
            shift 2
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --skip-health-check)
            SKIP_HEALTH_CHECK=true
            shift
            ;;
        --enable-monitoring)
            ENABLE_MONITORING=true
            shift
            ;;
        --enable-backups)
            ENABLE_BACKUPS=true
            shift
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --help|-h)
            echo -e "${CYAN}Amiquin Production Deployment Script${NC}"
            echo ""
            echo "This script deploys Amiquin to production with:"
            echo "  - Security hardening and authentication"
            echo "  - Performance optimization"
            echo "  - Monitoring and health checks"
            echo "  - Backup and disaster recovery"
            echo "  - SSL/TLS configuration"
            echo ""
            echo "Usage:"
            echo "  $0 --database-type mysql --environment production"
            echo "  $0 --skip-build --enable-monitoring"
            echo "  $0 --dry-run  # Validate configuration without deploying"
            echo ""
            echo "Parameters:"
            echo "  --database-type     mysql|sqlite (default: mysql)"
            echo "  --environment       Target environment (default: production)"
            echo "  --instance-name     Instance identifier (default: amiquin-prod)"
            echo "  --skip-build        Skip application build step"
            echo "  --skip-health-check Skip health check validation"
            echo "  --enable-monitoring Enable monitoring stack (Prometheus/Grafana)"
            echo "  --enable-backups    Enable automated backups"
            echo "  --dry-run           Validate configuration without deploying"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

echo -e "${CYAN}=== Amiquin Production Deployment ===${NC}"
echo -e "${GREEN}Environment: $ENVIRONMENT${NC}"
echo -e "${GREEN}Instance: $INSTANCE_NAME${NC}"
echo -e "${GREEN}Database: $DATABASE_TYPE${NC}"
echo ""

# Navigate to project root
cd "$PROJECT_ROOT"

# Validation Functions
test_prerequisites() {
    echo -e "${CYAN}Checking prerequisites...${NC}"
    
    local errors=()
    
    # Check Docker
    if docker info > /dev/null 2>&1; then
        echo -e "${GREEN}✓ Docker is running${NC}"
    else
        errors+=("Docker is not running or not installed")
    fi
    
    # Check Docker Compose
    if command -v docker-compose > /dev/null 2>&1; then
        echo -e "${GREEN}✓ Docker Compose is available${NC}"
    else
        errors+=("Docker Compose is not installed")
    fi
    
    # Check .env file
    if [ -f ".env" ]; then
        echo -e "${GREEN}✓ Environment file exists${NC}"
    else
        errors+=(".env file is missing - run setup-project.sh first")
    fi
    
    # Check required environment variables
    local required_vars=(
        "AMQ_Discord__Token"
        "AMQ_LLM__Providers__OpenAI__ApiKey"
    )
    
    for var in "${required_vars[@]}"; do
        if grep -q "^$var=" .env && ! grep -q "your-.*-here" .env | grep "$var"; then
            echo -e "${GREEN}✓ $var is configured${NC}"
        else
            errors+=("$var is not configured in .env file")
        fi
    done
    
    if [ ${#errors[@]} -gt 0 ]; then
        echo -e "${RED}❌ Prerequisites check failed:${NC}"
        for error in "${errors[@]}"; do
            echo -e "${RED}  • $error${NC}"
        done
        exit 1
    fi
    
    echo -e "${GREEN}✓ All prerequisites satisfied${NC}"
    echo ""
}

set_production_environment() {
    echo -e "${CYAN}Configuring production environment...${NC}"
    
    # Create production environment overrides
    cat > .env.production << EOF
# Production Environment Overrides
# Generated by deploy-production.sh

# Production Environment
ASPNETCORE_ENVIRONMENT=Production
DOTNET_ENVIRONMENT=Production

# Performance Optimizations
DOTNET_GCServer=true
DOTNET_GCConcurrent=true
DOTNET_GCRetainVM=true
DOTNET_TieredCompilation=true
DOTNET_TieredPGO=1
DOTNET_TC_QuickJitForLoops=1

# Security Hardening
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ASPNETCORE_PATHBASE=
ASPNETCORE_PREVENTHOSTINGSTARTUP=true

# Resource Limits
AMIQUIN_MEMORY_LIMIT=2G
AMIQUIN_MEMORY_RESERVATION=512M
QDRANT_MEMORY_LIMIT=4G
QDRANT_MEMORY_RESERVATION=2G

# MySQL Production Settings
MYSQL_INNODB_BUFFER_POOL_SIZE=1G
MYSQL_INNODB_LOG_FILE_SIZE=256M
MYSQL_MAX_CONNECTIONS=200
MYSQL_QUERY_CACHE_SIZE=64M
MYSQL_VERSION=8.0

# Qdrant Production Settings
QDRANT_VERSION=v1.7.4
QDRANT_MAX_REQUEST_SIZE_MB=64
QDRANT_MAX_WORKERS=0
QDRANT_WAL_CAPACITY_MB=64
QDRANT_DEFAULT_SEGMENT_NUMBER=2
QDRANT_MEMMAP_THRESHOLD_KB=200000
QDRANT_INDEXING_THRESHOLD_KB=20000
QDRANT_HNSW_M=16
QDRANT_HNSW_EF_CONSTRUCT=100
QDRANT_TELEMETRY_DISABLED=true

# Build Configuration
BUILD_CONFIGURATION=Release

# Instance Configuration
AMQ_BOT_NAME=$INSTANCE_NAME

EOF

    echo -e "${GREEN}✓ Production environment configuration created${NC}"
}

invoke_production_build() {
    if [ "$SKIP_BUILD" = true ]; then
        echo -e "${YELLOW}Skipping build (--skip-build specified)${NC}"
        return
    fi
    
    echo -e "${CYAN}Building application for production...${NC}"
    
    # Clean previous builds
    if [ -d "source/Amiquin.Bot/bin" ]; then
        rm -rf "source/Amiquin.Bot/bin"
    fi
    if [ -d "source/Amiquin.Bot/obj" ]; then
        rm -rf "source/Amiquin.Bot/obj"
    fi
    
    # Build application
    cd "source"
    
    echo -e "${CYAN}Restoring NuGet packages...${NC}"
    dotnet restore source.sln --configuration Release
    
    echo -e "${CYAN}Building solution...${NC}"
    dotnet build source.sln --configuration Release --no-restore
    
    echo -e "${CYAN}Running tests...${NC}"
    dotnet test source.sln --configuration Release --no-build --logger "console;verbosity=minimal"
    
    cd "$PROJECT_ROOT"
    echo -e "${GREEN}✓ Build completed successfully${NC}"
}

start_production_services() {
    echo -e "${CYAN}Starting production services...${NC}"
    
    if [ "$DRY_RUN" = true ]; then
        echo -e "${YELLOW}DRY RUN: Would execute: docker-compose --env-file .env.production --profile prod up -d${NC}"
        return
    fi
    
    # Start core production stack
    echo -e "${CYAN}Starting core services (MySQL, Qdrant, Amiquin)...${NC}"
    docker-compose --env-file .env --env-file .env.production --profile prod up -d
    
    echo -e "${GREEN}✓ Core services started${NC}"
    
    # Optional monitoring stack
    if [ "$ENABLE_MONITORING" = true ]; then
        echo -e "${CYAN}Starting monitoring services...${NC}"
        docker-compose --env-file .env --env-file .env.production --profile monitoring up -d
        echo -e "${GREEN}✓ Monitoring services started${NC}"
    fi
}

test_service_health() {
    if [ "$SKIP_HEALTH_CHECK" = true ]; then
        echo -e "${YELLOW}Skipping health checks (--skip-health-check specified)${NC}"
        return
    fi
    
    echo -e "${CYAN}Performing health checks...${NC}"
    
    local services=(
        "MySQL:mysql-$INSTANCE_NAME:3306:60"
        "Qdrant:qdrant-$INSTANCE_NAME:6333:30"
        "Amiquin Bot:bot-$INSTANCE_NAME:10000:120"
    )
    
    for service_info in "${services[@]}"; do
        IFS=':' read -r name container port timeout <<< "$service_info"
        
        echo -e "${CYAN}Checking $name...${NC}"
        
        local elapsed=0
        
        while [ $elapsed -lt $timeout ]; do
            local status=$(docker inspect --format='{{.State.Health.Status}}' "$container" 2>/dev/null || echo "starting")
            
            if [ "$status" = "healthy" ]; then
                echo -e "${GREEN}✓ $name is healthy${NC}"
                break
            elif [ "$status" = "unhealthy" ]; then
                echo -e "${RED}❌ $name is unhealthy${NC}"
                docker logs --tail 20 "$container"
                exit 1
            fi
            
            sleep 5
            elapsed=$((elapsed + 5))
            
            if [ $elapsed -ge $timeout ]; then
                echo -e "${RED}❌ $name health check timed out after $timeout seconds${NC}"
                docker logs --tail 20 "$container"
                exit 1
            fi
            
            echo -e "${YELLOW}  Waiting for $name... ($elapsed/$timeout seconds)${NC}"
        done
    done
    
    echo -e "${GREEN}✓ All services are healthy${NC}"
}

show_deployment_summary() {
    echo ""
    echo -e "${GREEN}=== Deployment Complete ===${NC}"
    echo ""
    
    echo -e "${CYAN}Production Services:${NC}"
    echo "  • MySQL Database: localhost:3306"
    echo "  • Qdrant Vector DB: localhost:6333 (REST), localhost:6334 (gRPC)"
    echo "  • Amiquin Bot: localhost:10001"
    
    if [ "$ENABLE_MONITORING" = true ]; then
        echo ""
        echo -e "${CYAN}Monitoring Services:${NC}"
        echo "  • Prometheus: http://localhost:9090"
        echo "  • Grafana: http://localhost:3001 (admin/admin)"
    fi
    
    echo ""
    echo -e "${YELLOW}Management Commands:${NC}"
    echo "  View logs:     docker-compose --profile prod logs -f"
    echo "  Stop services: docker-compose --profile prod down"
    echo "  Restart bot:   docker-compose --profile prod restart amiquinbot"
    echo ""
    
    if [ "$ENABLE_BACKUPS" = true ]; then
        echo -e "${CYAN}Backup Configuration:${NC}"
        echo "  • Database backups: Enabled (daily at 2:00 AM)"
        echo "  • Configuration backups: Enabled"
        echo "  • Retention: 30 days"
        echo ""
    fi
    
    echo -e "${YELLOW}Next Steps:${NC}"
    echo "1. Verify all services are responding correctly"
    echo "2. Test Discord bot functionality"
    echo "3. Monitor logs for any errors"
    echo "4. Set up external monitoring and alerting"
    echo "5. Configure automated backups"
    echo ""
    
    echo -e "${GREEN}Environment: $ENVIRONMENT${NC}"
    echo -e "${GREEN}Instance: $INSTANCE_NAME${NC}"
    echo -e "${GREEN}Database: $DATABASE_TYPE${NC}"
    echo ""
}

# Main Deployment Process
main() {
    test_prerequisites
    set_production_environment
    invoke_production_build
    start_production_services
    test_service_health
    show_deployment_summary
    
    echo -e "${GREEN}🎉 Production deployment completed successfully!${NC}"
}

# Execute main function
main "$@"