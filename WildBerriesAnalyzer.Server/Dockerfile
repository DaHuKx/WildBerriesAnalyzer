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
RUN dotnet publish "WildBerriesAnalyzer.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WildBerriesAnalyzer.Server.dll"]
