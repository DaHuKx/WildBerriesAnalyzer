# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["WildBerriesAnalyzer.Bots/WildBerriesAnalyzer.Bots.csproj", "WildBerriesAnalyzer.Bots/"]
COPY ["WildBerriesAnalyzer.Business/WildBerriesAnalyzer.Business.csproj", "WildBerriesAnalyzer.Business/"]
COPY ["WildBerriesAnalyzer.Data/WildBerriesAnalyzer.Data.csproj", "WildBerriesAnalyzer.Data/"]
COPY ["WildBerriesAnalyzer.Domain/WildBerriesAnalyzer.Domain.csproj", "WildBerriesAnalyzer.Domain/"]
RUN dotnet restore "WildBerriesAnalyzer.Bots/WildBerriesAnalyzer.Bots.csproj"

COPY . .
WORKDIR "/src/WildBerriesAnalyzer.Bots"
RUN dotnet build "WildBerriesAnalyzer.Bots.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "WildBerriesAnalyzer.Bots.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WildBerriesAnalyzer.Bots.dll"]
