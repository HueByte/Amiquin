#!/bin/bash

# Check if a migration name is provided
if [ -z "$1" ]; then
    echo "Error: Migration name is required."
    echo "Usage: $0 <MigrationName>"
    exit 1
fi

MIGRATION_NAME=$1

# Set the root directory
ROOT_DIR=$(cd "$(dirname "$0")/.." && pwd)

# Convert Cygwin/Git Bash paths to Windows-style paths if necessary
if [[ "$ROOT_DIR" == /cygdrive/* ]]; then
    ROOT_DIR=$(cygpath -w "$ROOT_DIR")
fi

# Define relative paths
STARTUP_PROJECT="./source/Amiquin.Bot"
INFRASTRUCTURE_PROJECT="./source/Amiquin.Infrastructure"
CONTEXT_NAME="AmiquinContext"
MIGRATION_DIR="$ROOT_DIR/source/Migrations"

echo "Root directory: $ROOT_DIR"
echo "Migration name: $MIGRATION_NAME"

# Set environment variable to skip database connection during migrations
export DOTNET_RUNNING_IN_CONTAINER=true

# Iterate through AMQ_DATABASE_MODE values
for MODE in 0 1 2 3; do
    export AMQ_DATABASE_MODE=$MODE
    echo "Running migration step with AMQ_DATABASE_MODE=$MODE"

    case $MODE in
        0)
            # SQLite
            (cd "$ROOT_DIR/$INFRASTRUCTURE_PROJECT" && dotnet ef migrations add "${MIGRATION_NAME}_SQLite" \
                --startup-project "$ROOT_DIR/$STARTUP_PROJECT" \
                --output-dir "$MIGRATION_DIR/Amiquin.Sqlite/Migrations" \
                --context $CONTEXT_NAME \
                --project ../Migrations/Amiquin.Sqlite \
                -- \
                --provider Sqlite)
            ;;
        1)
            # MySQL
            (cd "$ROOT_DIR/$INFRASTRUCTURE_PROJECT" && dotnet ef migrations add "${MIGRATION_NAME}_MySql" \
                --startup-project "$ROOT_DIR/$STARTUP_PROJECT" \
                --output-dir "$MIGRATION_DIR/Amiquin.MySql/Migrations" \
                --context $CONTEXT_NAME \
                --project ../Migrations/Amiquin.MySql)
            ;;
        2)
            # MSSQL
            (cd "$ROOT_DIR/$INFRASTRUCTURE_PROJECT" && dotnet ef migrations add "${MIGRATION_NAME}_MSSql" \
                --startup-project "$ROOT_DIR/$STARTUP_PROJECT" \
                --output-dir "$MIGRATION_DIR/Amiquin.MSSql/Migrations" \
                --context $CONTEXT_NAME \
                --project ../Migrations/Amiquin.MSSql \
                -- \
                --provider SqlServer)
            ;;
        3)
            # PostgreSQL
            (cd "$ROOT_DIR/$INFRASTRUCTURE_PROJECT" && dotnet ef migrations add "${MIGRATION_NAME}_Postgres" \
                --startup-project "$ROOT_DIR/$STARTUP_PROJECT" \
                --output-dir "$MIGRATION_DIR/Amiquin.Postgres/Migrations" \
                --context $CONTEXT_NAME \
                --project ../Migrations/Amiquin.Postgres \
                -- \
                --provider Npgsql)
            ;;
    esac
done

# Clean up environment variables
unset AMQ_DATABASE_MODE
unset DOTNET_RUNNING_IN_CONTAINER
echo "Environment variables have been removed."
