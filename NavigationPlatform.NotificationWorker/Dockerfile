FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NavigationPlatform.Domain/ NavigationPlatform.Domain/
COPY NavigationPlatform.Application/ NavigationPlatform.Application/
COPY NavigationPlatform.Infrastructure/ NavigationPlatform.Infrastructure/
COPY NavigationPlatform.NotificationWorker/ NavigationPlatform.NotificationWorker/

RUN dotnet restore NavigationPlatform.NotificationWorker/NavigationPlatform.NotificationWorker.csproj
RUN dotnet publish NavigationPlatform.NotificationWorker/NavigationPlatform.NotificationWorker.csproj \
    -c Release -o /app/publish 

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NavigationPlatform.NotificationWorker.dll"]
