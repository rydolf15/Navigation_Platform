FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NavigationPlatform.NotificationWorker/NavigationPlatform.NotificationWorker.csproj NavigationPlatform.NotificationWorker/
COPY src/journeys/NavigationPlatform.Journeys.Domain/NavigationPlatform.Domain.csproj src/journeys/NavigationPlatform.Journeys.Domain/

RUN dotnet restore NavigationPlatform.NotificationWorker/NavigationPlatform.NotificationWorker.csproj

COPY . .
RUN dotnet publish NavigationPlatform.NotificationWorker/NavigationPlatform.NotificationWorker.csproj \
    -c Release -o /app/publish 

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NavigationPlatform.NotificationWorker.dll"]
