# Scripts Reference

This document describes the available build and maintenance scripts for the Amiquin project.

## Setup Scripts

### `setup-project.ps1` / `setup-project.sh`
Interactive setup script for initial project configuration.

**Usage:**
```bash
# Windows
.\scripts\setup-project.ps1

# Linux/macOS
./scripts/setup-project.sh
```

**Options:**
- `-Help` / `--help`: Show help information
- `-Default` / `--default`: Use recommended defaults
- `-NonInteractive` / `--non-interactive`: Automated setup

**What it does:**
- Creates `.env` file with configuration
- Sets up `appsettings.json`
- Creates necessary data directories
- Builds the solution
- Provides next steps guidance

## Documentation Scripts

### `docfx.ps1` / `docfx.sh`
Build and serve API documentation.

**Usage:**
```bash
# Windows
.\scripts\docfx.ps1 build
.\scripts\docfx.ps1 serve

# Linux/macOS
./scripts/docfx.sh build
./scripts/docfx.sh serve
```

**Commands:**
- `build`: Generate documentation
- `serve`: Build and serve locally
- `clean`: Clean generated files

## Database Scripts

### `generate-migrations.sh`
Create new Entity Framework migrations.

**Usage:**
```bash
./scripts/generate-migrations.sh MigrationName
```

**Requirements:**
- Entity Framework tools installed
- Database provider configured

## Linting Scripts

### `markdownlint.ps1` / `markdownlint.sh`
Lint markdown files for consistency.

**Usage:**
```bash
# Windows
.\scripts\markdownlint.ps1

# Linux/macOS
./scripts/markdownlint.sh
```

**Configuration:**
- Rules defined in `dev/config/.markdownlint.json`
- Checks documentation and README files

## Utility Scripts

### `fix-line-endings.ps1`
Fix line endings in source files.

**Usage:**
```powershell
.\scripts\fix-line-endings.ps1
```

### `check-line-endings.ps1`
Check for inconsistent line endings.

**Usage:**
```powershell
.\scripts\check-line-endings.ps1
```

## CI/CD Integration

### GitHub Actions
Scripts are designed to work in CI/CD pipelines:

```yaml
- name: Setup Project
  run: ./scripts/setup-project.ps1 -NonInteractive
  
- name: Build Documentation
  run: ./scripts/docfx.ps1 build
  
- name: Lint Markdown
  run: ./scripts/markdownlint.ps1
```

### Prerequisites
- .NET 9 SDK
- DocFX (for documentation)
- Node.js (for markdown linting)
- PowerShell Core (cross-platform scripts)

## Script Development

### Guidelines
- Use PowerShell Core for cross-platform compatibility
- Provide both `.ps1` and `.sh` versions when possible
- Include help text and parameter validation
- Handle errors gracefully
- Use consistent output formatting

### Testing Scripts
- Test on multiple platforms
- Verify in clean environments
- Check error conditions
- Test CI/CD integration