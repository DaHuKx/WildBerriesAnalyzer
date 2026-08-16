# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["WildBerriesAnalyzer.Server/WildBerriesAnalyzer.Server.csproj", "WildBerriesAnalyzer.Server/"]
COPY ["WildBerriesAnalyzer.Business/WildBerriesAnalyzer.Business.csproj", "WildBerriesAnalyzer.Business/"]
COPY ["WildBerriesAnalyzer.Data/WildBerriesAnalyzer.Data.csproj", "WildBerriesAnalyzer.Data/"]
COPY ["WildBerriesAnalyzer.Domain/WildBerriesAnalyzer.Domain.csproj", "WildBerriesAnalyzer.Domain/"]
RUN dotnet restore "WildBerriesAnalyzer.Server/WildBerriesAnalyzer.Server.csproj"

COPY . .
WORKDIR "/src/WildBerriesAnalyzer.Server"
RUN dotnet build "WildBerriesAnalyzer.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# linux-x64 driver для Playwright (Ozon Chromium), даже если host-сборка иначе определит платформу.
RUN dotnet publish "WildBerriesAnalyzer.Server.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:PlaywrightPlatform=linux-x64

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
# Общий путь браузеров (не ~/.cache), чтобы процесс в контейнере их находил.
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

# Chromium + OS-зависимости для Ozon (Microsoft.Playwright 1.54).
# CLI берём из драйвера NuGet (.playwright), без PowerShell.
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends ca-certificates; \
    NODE="$(find /app/.playwright/node -type f -name node | head -n 1)"; \
    test -n "$NODE"; \
    chmod +x "$NODE"; \
    mkdir -p /ms-playwright; \
    "$NODE" /app/.playwright/package/cli.js install --with-deps chromium; \
    chmod -R a+rx /ms-playwright; \
    apt-get clean; \
    rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "WildBerriesAnalyzer.Server.dll"]
