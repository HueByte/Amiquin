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

# Iterate through AMQ_DATABASE_MODE values (corrected mapping)
for MODE in 1 0 2 3; do
    export AMQ_DATABASE_MODE=$MODE
    echo "Running migration step with AMQ_DATABASE_MODE=$MODE"

    case $MODE in
        1)
            # SQLite (Mode 1)
            (cd "$ROOT_DIR/$INFRASTRUCTURE_PROJECT" && dotnet ef migrations add "${MIGRATION_NAME}_SQLite" \
                --startup-project "$ROOT_DIR/$STARTUP_PROJECT" \
                --output-dir "$MIGRATION_DIR/Amiquin.Sqlite/Migrations" \
                --context $CONTEXT_NAME \
                --project ../Migrations/Amiquin.Sqlite \
                -- \
                --provider Sqlite)
            ;;
        0)
            # MySQL (Mode 0)
            (cd "$ROOT_DIR/$INFRASTRUCTURE_PROJECT" && dotnet ef migrations add "${MIGRATION_NAME}_MySql" \
                --startup-project "$ROOT_DIR/$STARTUP_PROJECT" \
                --output-dir "$MIGRATION_DIR/Amiquin.MySql/Migrations" \
                --context $CONTEXT_NAME \
                --project ../Migrations/Amiquin.MySql)
            ;;
        2)
            # PostgreSQL (Mode 2)
            (cd "$ROOT_DIR/$INFRASTRUCTURE_PROJECT" && dotnet ef migrations add "${MIGRATION_NAME}_Postgres" \
                --startup-project "$ROOT_DIR/$STARTUP_PROJECT" \
                --output-dir "$MIGRATION_DIR/Amiquin.Postgres/Migrations" \
                --context $CONTEXT_NAME \
                --project ../Migrations/Amiquin.Postgres \
                -- \
                --provider Npgsql)
            ;;
        3)
            # MSSQL (Mode 3)
            (cd "$ROOT_DIR/$INFRASTRUCTURE_PROJECT" && dotnet ef migrations add "${MIGRATION_NAME}_MSSql" \
                --startup-project "$ROOT_DIR/$STARTUP_PROJECT" \
                --output-dir "$MIGRATION_DIR/Amiquin.MSSql/Migrations" \
                --context $CONTEXT_NAME \
                --project ../Migrations/Amiquin.MSSql \
                -- \
                --provider SqlServer)
            ;;
    esac
done

# Clean up environment variables
unset AMQ_DATABASE_MODE
unset DOTNET_RUNNING_IN_CONTAINER
echo "Environment variables have been removed."
