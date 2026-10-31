# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY ["src/AdminDashboard/AdminDashboard.csproj", "AdminDashboard/"]
COPY ["src/NigerianNewsGrid.Client/NigerianNewsGrid.Client.csproj", "NigerianNewsGrid.Client/"]
RUN --mount=type=cache,id=nuget,sharing=locked,target=/root/.nuget/packages \
    dotnet restore "AdminDashboard/AdminDashboard.csproj" --disable-parallel

COPY src/ ./
WORKDIR /src/AdminDashboard
RUN --mount=type=cache,id=nuget,sharing=locked,target=/root/.nuget/packages \
    dotnet publish "AdminDashboard.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN mkdir -p /app/data/keys && chown -R $APP_UID:$APP_UID /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish ./

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "AdminDashboard.dll"]
