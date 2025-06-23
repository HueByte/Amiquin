# Generate Migrations Script

This script is used to generate Entity Framework Core migrations for multiple database contexts. It supports MySQL, SQLite, MSSQL, and PostgreSQL. The script iterates through different values of the `AMQ_DATABASE_MODE` environment variable and appends the mode to the migration name.

## Prerequisites

- Ensure you have the [.NET SDK](https://dotnet.microsoft.com/download) installed.
- Install the `dotnet-ef` tool globally if not already installed:
  
  ```bash
  dotnet tool install --global dotnet-ef
  ```

- Ensure you have the necessary database providers installed in your project.

## Usage

### On Linux/MacOS

1. Open a terminal and navigate to the `scripts` folder:

   ```bash
   cd /path/to/scripts
   ```

2. Run the script with the desired migration name:

   ```bash
   ./generate-migrations.sh <MigrationName>
   ```

   Replace `<MigrationName>` with the name of your migration (e.g., `InitialMigration`).

### On Windows

1. Open a Command Prompt or PowerShell and navigate to the `scripts` folder:

   ```cmd
   cd \path\to\scripts
   ```

2. Run the script using Git Bash or WSL (Windows Subsystem for Linux):

   ```bash
   ./generate-migrations.sh <MigrationName>
   ```

   Replace `<MigrationName>` with the name of your migration (e.g., `InitialMigration`).

## Example

To generate migrations with the name `InitialMigration`, run:

```bash
./generate-migrations.sh InitialMigration
```

This will create migrations for all supported databases, appending the `AMQ_DATABASE_MODE` value to the migration name (e.g., `InitialMigration_MySql0`, `InitialMigration_SQLite1`, etc.).

## Notes

- The script automatically cleans up the `AMQ_DATABASE_MODE` environment variable after execution.
- Ensure you have write permissions to the `Migrations` folder in the `source/Amiquin.Infrastructure` project.
