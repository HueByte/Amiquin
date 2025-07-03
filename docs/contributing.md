# Contributing to Amiquin

Thank you for your interest in contributing to Amiquin! This guide will help you get started with contributing to the project.

## Code of Conduct

By participating in this project, you agree to abide by our Code of Conduct:

- **Be respectful** to all community members
- **Be constructive** in discussions and feedback
- **Be helpful** to newcomers and fellow contributors
- **Be patient** with questions and different skill levels

## Ways to Contribute

### 1. Reporting Bugs

Help us improve by reporting bugs you encounter:

1. **Check existing issues** to avoid duplicates
2. **Use the bug report template** when creating issues
3. **Provide detailed information**:
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (OS, .NET version)
   - Error messages or logs

### 2. Suggesting Features

We welcome feature suggestions:

1. **Check the roadmap** for planned features
2. **Use the feature request template**
3. **Explain the use case** and benefits
4. **Consider implementation complexity**

### 3. Contributing Code

#### Prerequisites

Before contributing code, ensure you have:

- **.NET 9.0 SDK** installed
- **Git** for version control
- **Discord bot** for testing
- **Development environment** set up (see [Development Guide](development.html))

#### Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:

   ```bash
   git clone https://github.com/YOUR_USERNAME/Amiquin.git
   cd Amiquin
   ```

3. **Set up the development environment** (see [Development Guide](development.html))
4. **Create a feature branch**:

   ```bash
   git checkout -b feature/your-feature-name
   ```

#### Making Changes

1. **Follow coding standards** (see Code Style section)
2. **Write tests** for new functionality
3. **Update documentation** as needed
4. **Test your changes** thoroughly

#### Submitting Changes

1. **Commit your changes** with descriptive messages:

   ```bash
   git add .
   git commit -m "feat: add new command for user management"
   ```

2. **Push to your fork**:

   ```bash
   git push origin feature/your-feature-name
   ```

3. **Create a pull request** with:
   - Clear title and description
   - Reference to related issues
   - Screenshots if applicable

### 4. Documentation

Help improve our documentation:

- **Fix typos** and grammatical errors
- **Add examples** and clarifications
- **Update outdated information**
- **Translate documentation** to other languages

### 5. Testing

Contribute to test coverage:

- **Write unit tests** for existing code
- **Add integration tests** for new features
- **Test edge cases** and error scenarios
- **Performance testing** for optimization

## Code Style Guidelines

### General Principles

- **Consistency**: Follow existing code patterns
- **Readability**: Write self-documenting code
- **Simplicity**: Prefer simple, clear solutions
- **Performance**: Consider performance implications

### C# Style Guidelines

#### Naming Conventions

```csharp
// Classes: PascalCase
public class UserService { }

// Methods: PascalCase
public async Task GetUserAsync() { }

// Properties: PascalCase
public string UserName { get; set; }

// Fields: camelCase with underscore prefix for private
private readonly IUserRepository _userRepository;

// Constants: PascalCase
public const string DefaultPrefix = "!";

// Parameters and local variables: camelCase
public void ProcessUser(User user, string userName) { }
```

#### Code Organization

```csharp
// Using statements at the top
using System;
using System.Threading.Tasks;
using Discord;

namespace Amiquin.Bot.Commands
{
    // Class summary documentation
    /// <summary>
    /// Handles user-related commands and interactions.
    /// </summary>
    public class UserCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
    {
        // Fields first
        private readonly IUserService _userService;
        
        // Constructor
        public UserCommands(IUserService userService)
        {
            _userService = userService;
        }
        
        // Methods
        [SlashCommand("profile", "View user profile")]
        public async Task ProfileCommand(IUser user = null)
        {
            // Implementation
        }
    }
}
```

#### Formatting

- **Use EditorConfig**: The project includes `.editorconfig` for consistent formatting
- **Run formatter**: Use `dotnet format` before committing
- **Indentation**: 4 spaces (no tabs)
- **Line length**: Aim for 120 characters or less
- **Braces**: Opening brace on same line for methods, new line for classes

### Commit Message Format

Use [Conventional Commits](https://www.conventionalcommits.org/) format:

```xml
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

#### Types

- **feat**: New feature
- **fix**: Bug fix
- **docs**: Documentation changes
- **style**: Code style changes (formatting, etc.)
- **refactor**: Code refactoring
- **test**: Adding or updating tests
- **chore**: Build process or auxiliary tool changes

#### Examples

```sh
feat(commands): add user profile command

Add new slash command to display user profiles with statistics
and customizable display options.

Closes #123
```

```sh
fix(database): resolve connection timeout issues

Increase connection timeout and add retry logic for database
operations to handle temporary network issues.

Fixes #456
```

## Pull Request Process

### Before Submitting

1. **Ensure tests pass**: Run `dotnet test` locally
2. **Format code**: Run `dotnet format`
3. **Update documentation**: Include relevant docs updates
4. **Test thoroughly**: Test your changes in various scenarios

### Pull Request Guidelines

1. **Clear title**: Use descriptive title following conventional commits
2. **Detailed description**: Explain what changes were made and why
3. **Link issues**: Reference related issues using keywords (Fixes #123)
4. **Screenshots**: Include screenshots for UI changes
5. **Breaking changes**: Clearly mark any breaking changes

### Review Process

1. **Automated checks**: Ensure CI/CD passes
2. **Code review**: Address reviewer feedback promptly
3. **Testing**: Reviewers will test functionality
4. **Approval**: At least one maintainer approval required
5. **Merge**: Maintainers will merge approved PRs

## Development Workflow

### Branch Strategy

- **main**: Stable, production-ready code
- **develop**: Integration branch for features
- **feature/**: Feature development branches
- **hotfix/**: Critical bug fixes
- **release/**: Release preparation branches

### Testing Strategy

#### Unit Tests

```csharp
[Fact]
public async Task GetUserAsync_ValidId_ReturnsUser()
{
    // Arrange
    var userId = 123456789;
    var expectedUser = new User { DiscordId = userId };
    _mockRepository.Setup(r => r.GetUserAsync(userId))
                   .ReturnsAsync(expectedUser);
    
    // Act
    var result = await _userService.GetUserAsync(userId);
    
    // Assert
    Assert.Equal(expectedUser, result);
}
```

#### Integration Tests

Test complete workflows including database interactions and external services.

### Documentation Updates

When contributing, update relevant documentation:

- **Code comments**: Add XML documentation for public APIs
- **README files**: Update setup and usage instructions
- **API docs**: Update DocFX documentation
- **Change logs**: Add entries for significant changes

## Getting Help

### Resources

- **Documentation**: Check existing docs first
- **Issues**: Search existing issues for similar problems
- **Discussions**: Use GitHub Discussions for questions
- **Discord**: Join our development Discord server

### Asking Questions

When asking for help:

1. **Search first**: Check if the question was already answered
2. **Be specific**: Provide context and details
3. **Include code**: Share relevant code snippets
4. **Describe attempts**: What have you already tried?

### Mentoring

New contributors can get help from experienced developers:

- **Good first issues**: Look for issues labeled "good first issue"
- **Pair programming**: Request help with complex features
- **Code reviews**: Learn from feedback on your PRs

## Recognition

We appreciate all contributions! Contributors are recognized through:

- **Contributors section** in README
- **Release notes** mention significant contributions
- **Discord roles** for active contributors
- **Maintainer status** for long-term contributors

## License

By contributing to Amiquin, you agree that your contributions will be licensed under the [MIT License](../LICENSE).

Thank you for contributing to Amiquin! 🤖
