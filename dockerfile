FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /Amiquin

RUN apt-get -y update
RUN apt-get -y upgrade
RUN apt-get install -y ffmpeg
RUN apt-get install -y python3
RUN pip install piper-tts

# Copy the solution and project files to set up caching for dependencies
COPY source/source.sln ./
COPY source/Amiquin.Bot/Amiquin.Bot.csproj ./Amiquin.Bot/
COPY source/Amiquin.Core/Amiquin.Core.csproj ./Amiquin.Core/
COPY source/Amiquin.Infrastructure/Amiquin.Infrastructure.csproj ./Amiquin.Infrastructure/

# Restore dependencies using the solution file
RUN dotnet restore source.sln

# Copy the rest of the source code
COPY source/ ./

# Build and publish the application in Release configuration
RUN dotnet publish Amiquin.Bot/Amiquin.Bot.csproj -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /Amiquin
COPY --from=build /Amiquin/out ./

# Define build-time argument and set it as an environment variable
ARG LOGS_PATH=/Amiquin/Data/Logs
ENV LOGS_PATH=${LOGS_PATH}
RUN mkdir -p ${LOGS_PATH}

# Run the application
ENTRYPOINT ["dotnet", "Amiquin.Bot.dll"]
