# Database Connection Strings - Provider-Specific Configuration

This document explains the new provider-specific connection string configuration in Amiquin.

## Overview

Amiquin now supports provider-specific connection strings for better configuration management and clearer separation between different database providers.

## Configuration Methods

### 1. Provider-Specific Connection Strings (Recommended)

Use provider-specific connection string names for clearer configuration:

```json
{
  "ConnectionStrings": {
    "Amiquin-Sqlite": "Data Source=Data/Database/amiquin.db",
    "Amiquin-Mysql": "Server=localhost;Database=amiquin;User=amiquin;Password=your_password;Pooling=True;"
  },
  "Database": {
    "Mode": 1
  }
}
```

### 2. Environment Variables

```bash
# Provider-specific (Recommended)
AMQ_ConnectionStrings__Amiquin-Sqlite=Data Source=Data/Database/amiquin.db
AMQ_ConnectionStrings__Amiquin-Mysql=Server=localhost;Database=amiquin;User=amiquin;Password=your_password;Pooling=True;

# Legacy (Still supported)
AMQ_ConnectionStrings__AmiquinContext=Data Source=Data/Database/amiquin.db
```

### 3. Docker Compose Integration

The provider-specific connection strings work seamlessly with Docker:

```bash
# Docker MySQL example
AMQ_ConnectionStrings__Amiquin-Mysql=Server=mysql-${AMQ_BOT_NAME};Database=${AMQ_DB_NAME};User=${AMQ_DB_USER};Password=${AMQ_DB_USER_PASSWORD};Pooling=True;
```

## Database Mode Configuration

Set the database mode to specify which provider to use:

- `Mode: 0` - MySQL (uses `Amiquin-Mysql` connection string)
- `Mode: 1` - SQLite (uses `Amiquin-Sqlite` connection string)
- `Mode: 2` - PostgreSQL (future support)
- `Mode: 3` - MSSQL (future support)

## Connection String Priority

The setup methods use the following priority order:

1. **Provider-specific environment variable**
   - `AMQ_ConnectionStrings__Amiquin-Sqlite` (for SQLite)
   - `AMQ_ConnectionStrings__Amiquin-Mysql` (for MySQL)

2. **Legacy environment variables**
   - `AMQ_SQLITE_PATH` (for SQLite)
   - `AMQ_DB_CONNECTION_STRING` (general)

3. **Provider-specific configuration section**
   - `ConnectionStrings:Amiquin-Sqlite`
   - `ConnectionStrings:Amiquin-Mysql`

4. **Legacy configuration section**
   - `ConnectionStrings:AmiquinContext`

5. **Database options configuration**
   - `Database:ConnectionString`

6. **Default connection string**
   - Built-in defaults for each provider

## Examples

### SQLite Configuration

```json
{
  "Database": { "Mode": 1 },
  "ConnectionStrings": {
    "Amiquin-Sqlite": "Data Source=Data/Database/amiquin.db"
  }
}
```

### MySQL Configuration

```json
{
  "Database": { "Mode": 0 },
  "ConnectionStrings": {
    "Amiquin-Mysql": "Server=localhost;Database=amiquin;User=amiquin;Password=secure_password;Pooling=True;"
  }
}
```

### Mixed Environment (Development)

You can have both connection strings defined for easy switching:

```json
{
  "Database": { "Mode": 1 },
  "ConnectionStrings": {
    "Amiquin-Sqlite": "Data Source=Data/Database/amiquin.db",
    "Amiquin-Mysql": "Server=localhost;Database=amiquin_dev;User=amiquin;Password=dev_password;Pooling=True;"
  }
}
```

## Migration Impact

### Existing Configurations

All existing configurations using `AmiquinContext` will continue to work unchanged. The new system is fully backward compatible.

### New Installations

The setup scripts have been updated to generate provider-specific connection strings by default, with legacy fallbacks for compatibility.

## Setup Script Changes

Both PowerShell (`setup-project.ps1`) and Bash (`setup-project.sh`) scripts now:

1. Generate provider-specific connection strings in `.env` files
2. Create `appsettings.json` with both provider-specific and legacy connection strings
3. Maintain full backward compatibility

## Benefits

1. **Clearer Configuration**: Each database provider has its own connection string
2. **Better Organization**: Easier to manage multiple database configurations
3. **Environment Flexibility**: Switch between providers without changing connection strings
4. **Docker Integration**: Works seamlessly with containerized deployments
5. **Backward Compatibility**: Existing configurations continue to work

## Troubleshooting

### Connection String Not Found

If you see connection issues:

1. Check the database mode matches your intended provider
2. Verify the correct provider-specific connection string is set
3. Ensure environment variables use the correct format
4. Check the priority order - higher priority settings override lower ones

### Legacy Configuration

To keep using the old format, simply continue using `AmiquinContext` in your connection strings. The system will automatically fall back to this configuration.

## Future Enhancements

- PostgreSQL support (`Amiquin-Postgres`)
- MSSQL support (`Amiquin-Mssql`)
- Additional configuration validation
- Provider-specific optimizations
