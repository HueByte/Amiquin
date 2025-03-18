# ☁️ Amiquin ☁️

<p align="center">
    <img src="./Assets/bannah.gif" alt="Amiquin" width="100%"/>
</p>

Amiquin is a modular and extensible application designed to streamline development with a focus on configurability, logging, and dependency injection. This project leverages modern .NET technologies to provide a solid foundation for building applications.
The goal is to create a robust, fun and scalable bot.

## ⚗️ Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (version 9.0 or later)
- [Docker](https://www.docker.com/) (optional)
- [ffmpeg](https://ffmpeg.org/download.html)
- [Piper](https://github.com/rhasspy/piper)

## ✨ Installation

### 🚢 Docker

Docker is recommended for running the application in a containerized environment. (Docker required)

1. Clone the repository:

```bash
git clone https://github.com/your-repo/amiquin.git
cd amiquin
```

2. Configure the application:
   - Copy the `.env.example` file to `.env`. Update the values as needed.
   - Copy the `appsettings.example.json` file to `appsettings.json`. Update the values as needed.

3. Run docker:

```bash
docker-compose up
```

### 👨‍💻 Local

If you want to run the application locally, follow the steps below. (You have to install the prerequisites)

> Install the prerequisites before running the application.
> Piper for the Text to Speech (TTS) feature.
> ffmpeg for the audio streaming to voicechat feature.

1. Clone the repository:

```bash
git clone https://github.com/your-repo/amiquin.git
cd amiquin
```

2. Restore dependencies:

```bash
dotnet restore
```

3. Configure the application:
   - Copy the `.env.example` file to `.env`. Update the values as needed.
   - Copy the `appsettings.example.json` file to `appsettings.json`. Update the values as needed.

4. Build the application:

```bash
dotnet run --project source/Amiquin.Bot -c Release
```

or if you want to create a self-contained executable:\
[Publish Documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish)

```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

### ⚙️ Configuration

The application uses `appsettings.json` and `.env` files for configuration. Ensure those files exist.
The templates are provided via `appsettings.example.json` and `.env.example`.

You can override settings using command-line arguments.

Required parameters:

- `Bot:Token` (appsettings.json) or `BOT_TOKEN` (.env) - Discord bot token.
- `Bot:OpenAIKey` (appsettings.json) or `OPEN_AI_KEY` (.env) - OpenAI API key.

> **Note:** the `.env` file is used only for docker-compose configuration.

## 📜 Logging

Logs are written to the console and a rolling log file located in the directory specified by `SQLITE_PATH` environment variable or in the application root `/Logs` directory.

## ☁️ Project Structure

- **source/Amiquin.Bot**: Entry point and application layer.
- **source/Amiquin.Core**: (BL) Core functionalities and shared logic.
- **source/Amiquin.Infrastructure**: (DAL) External services logic.

### 🕵️ Detailed

> `Amiquin.Bot`

- **source/Amiquin.Bot.Configurators**: Dependency injection and configuration setup.
- **source/Amiquin.Bot.Commands**: Discord commands.
- **source/Amiquin.Bot.Console**: Console related features like Spectre.
- **source/Amiquin.Bot.Messages**: Markdown files that are moved on build and utilised by Amiquin.

> `Amiquin.Core`

- **source/Amiquin.Core.Models**: Shared ORM models.
- **source/Amiquin.Core.Abstraction**: Abstractions.
- **source/Amiquin.Core.Services**: Core services.
- **source/Amiquin.Core.Utilities**: Utility classes.
- **source/Amiquin.Core.Extensions**: Extension methods.
- **source/Amiquin.Core.Attributes**: Attributes.
- **source/Amiquin.Core.Discord**: Discord specific features.
- **source/Amiquin.Core.Options**: Options for options pattern.

> `Amiquin.Infrastructure`

- **source/Amiquin.Infrastructure.Repositories**: Repositories.
- **source/Amiquin.Infrastructure.Migrations**: EFC migrations.

## 🫂 Contributing

Contributions are welcome! Please fork the repository and submit a pull request with your changes.

## 🪪 License

This project is licensed under the [MIT License](LICENSE).

## 💖 Acknowledgments

- [Serilog](https://serilog.net/) for robust logging capabilities.
- [SpectreConsole](https://spectreconsole.net/quick-start) for beautiful console output.
- [Discord.NET](https://github.com/discord-net/Discord.Net) for Discord bot integration.
