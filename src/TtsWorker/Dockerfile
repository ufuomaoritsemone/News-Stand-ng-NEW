# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY ["src/TtsWorker/TtsWorker.csproj", "TtsWorker/"]
COPY ["src/NigerianNewsGrid.Client/NigerianNewsGrid.Client.csproj", "NigerianNewsGrid.Client/"]
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore "TtsWorker/TtsWorker.csproj"

COPY src/ ./
WORKDIR /src/TtsWorker
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "TtsWorker.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
RUN mkdir -p /app/data/audio && chown -R $APP_UID:$APP_UID /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish ./
USER $APP_UID

ENTRYPOINT ["dotnet", "TtsWorker.dll"]
