# См. статью по ссылке https://aka.ms/customizecontainer, чтобы узнать как настроить контейнер отладки и как Visual Studio использует этот Dockerfile для создания образов для ускорения отладки.

# Этот этап используется при запуске из VS в быстром режиме (по умолчанию для конфигурации отладки)
FROM mcr.microsoft.com/dotnet/runtime:8.0-noble AS base
USER $APP_UID
WORKDIR /app


# Этот этап используется для сборки проекта службы
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["WildBerriesAnalyzer.VkAddProductBot/WildBerriesAnalyzer.VkAddProductBot.csproj", "WildBerriesAnalyzer.VkAddProductBot/"]
COPY ["WildBerriesAnalyzer.Business/WildBerriesAnalyzer.Business.csproj", "WildBerriesAnalyzer.Business/"]
COPY ["WildBerriesAnalyzer.Data/WildBerriesAnalyzer.Data.csproj", "WildBerriesAnalyzer.Data/"]
COPY ["WildBerriesAnalyzer.Domain/WildBerriesAnalyzer.Domain.csproj", "WildBerriesAnalyzer.Domain/"]
RUN dotnet restore "./WildBerriesAnalyzer.VkAddProductBot/WildBerriesAnalyzer.VkAddProductBot.csproj"
COPY . .
WORKDIR "/src/WildBerriesAnalyzer.VkAddProductBot"
RUN dotnet build "./WildBerriesAnalyzer.VkAddProductBot.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Этот этап используется для публикации проекта службы, который будет скопирован на последний этап
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./WildBerriesAnalyzer.VkAddProductBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Этот этап используется в рабочей среде или при запуске из VS в обычном режиме (по умолчанию, когда конфигурация отладки не используется)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WildBerriesAnalyzer.VkAddProductBot.dll"]