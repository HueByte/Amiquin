FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG MAIN_WORK_DIR=/home/app/amiquin
WORKDIR ${MAIN_WORK_DIR}

RUN apt-get -y update && \
    apt-get -y upgrade && \
    apt-get install -y \
    build-essential \
    gcc \
    g++ \
    curl \
    tar \
    pkg-config \
    libtool \
    m4

# Install libsodium
RUN mkdir -p ${MAIN_WORK_DIR}/libsodium
WORKDIR ${MAIN_WORK_DIR}/libsodium
RUN curl -L https://download.libsodium.org/libsodium/releases/LATEST.tar.gz -o libsodium.tar.gz && \
    tar xzf libsodium.tar.gz && \
    cd libsodium-* && \
    gcc --version && \
    ./configure && \
    make -j$(nproc) && \
    make install && \
    ldconfig && \
    cd .. && \
    rm -rf libsodium.tar.gz libsodium-*

# Install libopus
RUN mkdir -p ${MAIN_WORK_DIR}/libopus
WORKDIR ${MAIN_WORK_DIR}/libopus
RUN curl -L https://ftp.osuosl.org/pub/xiph/releases/opus/opus-1.5.tar.gz -o opus.tar.gz && \
    tar xzf opus.tar.gz && \
    cd opus-1.5 && \
    ./configure && \
    make -j$(nproc) && \
    make install && \
    ldconfig && \
    cd .. && \
    rm -rf opus.tar.gz opus-1.5

WORKDIR ${MAIN_WORK_DIR}

# Copy the solution and project files to set up caching for dependencies
COPY source/source.sln ./
COPY source/Amiquin.Bot/Amiquin.Bot.csproj ./Amiquin.Bot/
COPY source/Amiquin.Core/Amiquin.Core.csproj ./Amiquin.Core/
COPY source/Amiquin.Infrastructure/Amiquin.Infrastructure.csproj ./Amiquin.Infrastructure/

# Restore dependencies using the solution file
RUN dotnet restore source.sln

# Copy the rest of the source code
COPY source/ ./

# Build and publish the application in Release configuration, outputting to "build"
RUN dotnet publish Amiquin.Bot/Amiquin.Bot.csproj -c Release -o build

# Stage 2: Create the runtime image and copy required files/libraries
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
RUN useradd -ms /bin/bash amiquin
ARG MAIN_WORK_DIR=/home/app/amiquin
WORKDIR ${MAIN_WORK_DIR}

RUN dpkg --add-architecture arm64
RUN apt-get update && apt-get install -y ffmpeg \
    libc6:arm64 \
    python3-full \
    python3-pip \
    pipx \
    build-essential

RUN pipx install piper-tts

# Copy the published build output
COPY --from=build ${MAIN_WORK_DIR}/build ${MAIN_WORK_DIR}/build

# Copy system libraries from the build stage (libsodium, libopus) and piper
COPY --from=build /usr/local/lib /usr/local/lib

# Set up log directory
ARG LOGS_PATH=${MAIN_WORK_DIR}/Data/Logs
ENV LOGS_PATH=${LOGS_PATH}
RUN mkdir -p ${LOGS_PATH}

WORKDIR ${MAIN_WORK_DIR}/build

# Update the dynamic linker run-time bindings
RUN ldconfig

USER amiquin
RUN pipx ensurepath

USER root

# Run the application
ENTRYPOINT ["dotnet", "Amiquin.Bot.dll"]