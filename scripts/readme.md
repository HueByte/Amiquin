# Amiquin Scripts

This directory contains utility scripts for the Amiquin project.

## Migration Scripts

### generate-migration.ps1 ⭐ **RECOMMENDED**

**Unified migration generation script** - Generates Entity Framework migrations for all supported database providers without requiring live database connections.

#### Features

- ✅ **Design-time safe** - No database connection required
- ✅ **Multi-provider support** - SQLite, MySQL, PostgreSQL, SQL Server
- ✅ **Single script** - Replaces multiple provider-specific scripts
- ✅ **Enhanced output** - Color-coded progress and detailed summaries
- ✅ **Flexible targeting** - Generate for specific provider or all providers

#### Usage

```powershell
# Generate SQLite migration (default)
.\generate-migration.ps1 AddChatSessionSupport

# Generate MySQL migration only
.\generate-migration.ps1 AddChatSessionSupport -Provider mysql

# Generate for all supported providers
.\generate-migration.ps1 AddChatSessionSupport -Provider all

# Enable verbose output for debugging
.\generate-migration.ps1 AddChatSessionSupport -Provider all -Verbose

# Show help
.\generate-migration.ps1 -Help
```

#### Supported Providers

- `sqlite` (default) - SQLite database
- `mysql` - MySQL database  
- `postgresql` - PostgreSQL database
- `sqlserver` - SQL Server database
- `all` - Generate migrations for all providers

#### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- Entity Framework Core Tools:

  ```powershell
  dotnet tool install --global dotnet-ef
  ```

### generate-migrations.ps1 / generate-migrations.sh

**Legacy multi-provider scripts** - Original scripts for generating migrations. The new unified `generate-migration.ps1` script is recommended for all new development.

### 2. Markdownlint Scripts

These scripts run markdownlint on all Markdown files in the repository to ensure consistent formatting and catch common issues.

#### Prerequisites

- [Node.js](https://nodejs.org/) must be installed
- The scripts will automatically install `markdownlint-cli` if not present

#### Available Scripts

##### PowerShell Script (`markdownlint.ps1`)

For Windows users with PowerShell:

```powershell
# Basic usage - lint all markdown files
.\markdownlint.ps1

# Auto-fix issues
.\markdownlint.ps1 -Fix

# Lint specific path
.\markdownlint.ps1 -Path "dev/docs/*.md"

# Verbose output
.\markdownlint.ps1 -Verbose

# Combined options
.\markdownlint.ps1 -Fix -Verbose
```

##### Bash Script (`markdownlint.sh`)

For Linux/MacOS/WSL users:

```bash
# Basic usage - lint all markdown files
./markdownlint.sh

# Auto-fix issues
./markdownlint.sh --fix

# Lint specific path
./markdownlint.sh dev/docs/*.md

# Verbose output
./markdownlint.sh --verbose

# Combined options
./markdownlint.sh --fix --verbose

# Show help
./markdownlint.sh --help
```

##### Windows CMD Batch File (`markdownlint.cmd`)

For Windows users who prefer Command Prompt:

```cmd
REM Basic usage
markdownlint.cmd

REM With parameters (passed to PowerShell script)
markdownlint.cmd -Fix
markdownlint.cmd -Path "dev/docs/*.md"
```

#### Testing Scripts

##### Test Script (`test-markdownlint.ps1`)

Tests both PowerShell and Bash markdownlint scripts:

```powershell
# Test both scripts
.\test-markdownlint.ps1

# Skip Bash test (Windows without WSL/Git Bash)
.\test-markdownlint.ps1 -SkipBash
```

#### Features

- **Automatic Installation**: Installs `markdownlint-cli` if not present
- **Configuration Support**: Uses `dev/.markdownlint.json` configuration file (with fallback to legacy `.markdownlint.json`)
- **Auto-fix**: Can automatically fix many common issues
- **Ignore Patterns**: Automatically ignores build directories and node_modules
- **Colored Output**: Easy-to-read colored terminal output
- **Cross-platform**: Works on Windows (PowerShell), Linux, and macOS

#### Configuration

The scripts use the `dev/.markdownlint.json` configuration file in the repository. This file contains rules for:

- Line length limits (disabled)
- HTML tags in markdown (allowed)
- Heading styles
- List formatting
- And more...

### 3. DocFX Documentation Scripts

These scripts run DocFX to build, serve, and manage documentation for the Amiquin project.

#### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) must be installed
- The scripts will automatically install DocFX if not present
- DocFX configuration file must exist at `dev/docfx.json`

#### Available Scripts

##### PowerShell Script (`docfx.ps1`)

For Windows users with PowerShell:

```powershell
# Basic usage - build documentation
.\docfx.ps1

# Build and serve on default port (8080)
.\docfx.ps1 -Action Serve

# Build and serve on custom port
.\docfx.ps1 -Action Serve -Port 3000

# Clean generated files
.\docfx.ps1 -Action Clean

# Force rebuild with verbose output
.\docfx.ps1 -Force -Verbose

# Initialize new DocFX project
.\docfx.ps1 -Action Init

# Use custom configuration file
.\docfx.ps1 -ConfigPath "custom/docfx.json"
```

##### Bash Script (`docfx.sh`)

For Linux/macOS/WSL users:

```bash
# Basic usage - build documentation
./docfx.sh

# Build and serve on default port
./docfx.sh serve

# Build and serve on custom port
./docfx.sh serve --port 3000

# Clean generated files
./docfx.sh clean

# Force rebuild with verbose output
./docfx.sh build --force --verbose

# Initialize new DocFX project
./docfx.sh init

# Use custom configuration file
./docfx.sh build --config "custom/docfx.json"

# Show help
./docfx.sh --help
```

##### Windows CMD Batch File (`docfx.cmd`)

For Windows users who prefer Command Prompt:

```cmd
REM Basic usage
docfx.cmd

REM With parameters (passed to PowerShell script)
docfx.cmd -Action Serve
docfx.cmd -Action Clean
```

#### Features

- **Automatic Installation**: Installs DocFX if not present
- **Multiple Actions**: Build, serve, clean, or initialize projects
- **Flexible Configuration**: Custom config file paths supported
- **Development Server**: Built-in HTTP server for local development
- **Force Rebuild**: Clean and rebuild option
- **Verbose Output**: Detailed build information when needed
- **Cross-platform**: Works on Windows (PowerShell), Linux, and macOS

#### Actions

- **Build** (default): Compile documentation from source
- **Serve**: Build and start local development server
- **Clean**: Remove all generated files
- **Init**: Create a new DocFX project template

#### Configuration

The scripts use the DocFX configuration file at `dev/docfx.json` by default. This file contains:

- Source code paths for API documentation
- Content paths for manual documentation
- Output directory settings
- Template and styling options
- Metadata and branding configuration

## Running Scripts

### From Repository Root

You can run any script from the repository root:

```bash
# Run migrations script
./scripts/generate-migrations.sh InitialMigration

# Run markdownlint (Bash)
./scripts/markdownlint.sh

# Run markdownlint (PowerShell)
./scripts/markdownlint.ps1

# Run docfx (Bash)
./scripts/docfx.sh

# Run docfx (PowerShell)
./scripts/docfx.ps1
```

### From Scripts Directory

Or navigate to the scripts directory first:

```bash
cd scripts

# Then run any script
./generate-migrations.sh InitialMigration
./markdownlint.sh --fix
./docfx.sh
```

## Integration with GitHub Actions

These scripts are integrated with the GitHub Actions workflows:

- **Markdownlint**: Automatically runs on all PRs and pushes that modify `.md` files
- **Migrations**: Can be run manually during development
- **DocFX**: Automatically builds and serves documentation on push and PR events

See `.github/workflows/` for the complete CI/CD pipeline.
