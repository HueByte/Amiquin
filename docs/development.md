# Development Guide

This guide covers setting up a local development environment for Amiquin and contributing to the project.

## Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 9.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Git** - [Download here](https://git-scm.com/)
- **Docker** (optional) - [Download here](https://www.docker.com/get-started)
- **IDE/Editor** - Visual Studio, VS Code, or JetBrains Rider

## Setting Up Development Environment

### 1. Clone the Repository

```bash
git clone https://github.com/huebyte/Amiquin.git
cd Amiquin
```

### 2. Configure Environment

```bash
# Copy example configuration
cp source/Amiquin.Bot/appsettings.example.json source/Amiquin.Bot/appsettings.json
cp .env.example .env

# Edit configuration files with your settings
```

### 3. Install Dependencies

```bash
# Restore NuGet packages
dotnet restore source/source.sln
```

### 4. Create Discord Bot Application

1. Go to [Discord Developer Portal](https://discord.com/developers/applications)
2. Create a new application
3. Navigate to the "Bot" section
4. Create a bot and copy the token
5. Update your `appsettings.json` or `.env` file with the token

### 5. Database Setup

#### Option A: SQLite (Default)

No additional setup required. The database will be created automatically.

#### Option B: MySQL (Docker)

```bash
# Start MySQL container
docker-compose up -d mysql

# Update your configuration to use MySQL
```

### 6. Build and Run

```bash
# Build the solution
dotnet build source/source.sln

# Run the bot
dotnet run --project source/Amiquin.Bot
```

## Project Structure

```
Amiquin/
├── source/
│   ├── Amiquin.Bot/          # Main bot application
│   │   ├── Commands/         # Slash commands and command handlers
│   │   ├── Configurators/    # Dependency injection setup
│   │   ├── Messages/         # Bot personality and messages
│   │   └── Preconditions/    # Command preconditions
│   ├── Amiquin.Core/         # Core business logic
│   │   ├── Models/           # Domain models
│   │   ├── Services/         # Business services
│   │   └── IRepositories/    # Repository interfaces
│   ├── Amiquin.Infrastructure/ # Data access layer
│   │   └── Repositories/     # Repository implementations
│   └── Migrations/           # Database migrations
├── docs/                     # Documentation
├── .github/                  # CI/CD workflows
└── docker-compose.yml        # Docker setup
```

## Development Workflow

### 1. Creating a New Feature

```bash
# Create a feature branch
git checkout -b feature/your-feature-name

# Make your changes
# ... code changes ...

# Commit your changes
git add .
git commit -m "feat: add new feature"

# Push to GitHub
git push origin feature/your-feature-name
```

### 2. Adding New Commands

1. Create a new command class in `source/Amiquin.Bot/Commands/`
2. Inherit from `InteractionModuleBase<ExtendedShardedInteractionContext>`
3. Use `[SlashCommand]` attribute for slash commands
4. Add any required services via dependency injection

Example:

```csharp
[Group("example", "Example command group")]
public class ExampleCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    [SlashCommand("hello", "Say hello")]
    public async Task HelloCommand()
    {
        await RespondAsync("Hello, world!");
    }
}
```

### 3. Adding New Services

1. Create interface in `source/Amiquin.Core/Services/`
2. Implement service in `source/Amiquin.Core/Services/`
3. Register in `source/Amiquin.Bot/Configurators/InjectionConfigurator.cs`

### 4. Database Changes

1. Update models in `source/Amiquin.Core/Models/`
2. Create migration:
   ```bash
   dotnet ef migrations add YourMigrationName --project source/Migrations/Amiquin.Sqlite
   ```
3. Update database:
   ```bash
   dotnet ef database update --project source/Migrations/Amiquin.Sqlite
   ```

## Code Style Guidelines

### General Principles

- Follow C# naming conventions
- Use meaningful variable and method names
- Write clear, concise comments
- Keep methods focused and small
- Use dependency injection for services

### Formatting

The project uses `.editorconfig` for consistent formatting. Run:

```bash
dotnet format source/source.sln
```

### Commit Messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` for new features
- `fix:` for bug fixes
- `docs:` for documentation changes
- `refactor:` for code refactoring
- `test:` for adding tests

## Testing

### Running Tests

```bash
# Run all tests
dotnet test source/source.sln

# Run with coverage
dotnet test source/source.sln --collect:"XPlat Code Coverage"
```

### Writing Tests

1. Create test classes in appropriate test projects
2. Use xUnit framework
3. Follow AAA pattern (Arrange, Act, Assert)
4. Mock external dependencies

## Debugging

### Visual Studio/VS Code

1. Set breakpoints in your code
2. Press F5 to start debugging
3. The bot will start with debugger attached

### Docker Debugging

```bash
# Build debug image
docker build -t amiquin:debug .

# Run with debug ports exposed
docker run -p 5000:5000 amiquin:debug
```

## Contributing

1. **Fork the repository** on GitHub
2. **Create a feature branch** from `main`
3. **Make your changes** following the style guidelines
4. **Add tests** for new functionality
5. **Ensure all tests pass** and code is formatted
6. **Create a pull request** with a clear description

### Pull Request Process

1. Update documentation if needed
2. Add tests for new features
3. Ensure CI/CD passes
4. Request review from maintainers
5. Address any feedback
6. Merge after approval

## Useful Commands

```bash
# Build solution
dotnet build source/source.sln

# Run bot locally
dotnet run --project source/Amiquin.Bot

# Format code
dotnet format source/source.sln

# Run tests
dotnet test source/source.sln

# Create migration
dotnet ef migrations add MigrationName --project source/Migrations/Amiquin.Sqlite

# Update database
dotnet ef database update --project source/Migrations/Amiquin.Sqlite

# Build Docker image
docker build -t amiquin .

# Run with Docker Compose
docker-compose up -d
```

## Getting Help

- **Documentation**: Check this documentation and inline code comments
- **Issues**: Create an issue on GitHub for bugs or feature requests
- **Discussions**: Use GitHub Discussions for questions and ideas
- **Discord**: Join our development Discord server for real-time help
