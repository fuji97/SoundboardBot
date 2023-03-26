FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/SoundboardBot.ApiClient/SoundboardBot.ApiClient.csproj", "SoundboardBot.ApiClient/"]
COPY ["src/SoundboardBot.Discord/SoundboardBot.Discord.csproj", "SoundboardBot.Discord/"]
RUN dotnet nuget add source https://www.myget.org/F/discord-net/api/v3/index.json --name MyGet-DiscordNET
RUN dotnet restore "SoundboardBot.ApiClient/SoundboardBot.ApiClient.csproj"
RUN dotnet restore "SoundboardBot.Discord/SoundboardBot.Discord.csproj"
COPY src/ .
WORKDIR "/src/SoundboardBot.Discord"
RUN dotnet build "SoundboardBot.Discord.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SoundboardBot.Discord.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS dependencies
WORKDIR /app
RUN apt-get update && apt-get install -y ffmpeg libopus-dev libsodium-dev
RUN ln -s "/usr/lib/x86_64-linux-gnu/libsodium.so" "./libsodium.so"
RUN ln -s "/usr/lib/x86_64-linux-gnu/libopus.so" "./opus.so"

FROM dependencies AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "SoundboardBot.Discord.dll"]
