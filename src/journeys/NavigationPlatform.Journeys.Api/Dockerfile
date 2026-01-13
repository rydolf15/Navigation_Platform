# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/journeys/NavigationPlatform.Journeys.Api/NavigationPlatform.Api.csproj src/journeys/NavigationPlatform.Journeys.Api/
COPY src/journeys/NavigationPlatform.Journeys.Application/NavigationPlatform.Application.csproj src/journeys/NavigationPlatform.Journeys.Application/
COPY src/journeys/NavigationPlatform.Journeys.Domain/NavigationPlatform.Domain.csproj src/journeys/NavigationPlatform.Journeys.Domain/
COPY src/journeys/NavigationPlatform.Journeys.Infrastructure/NavigationPlatform.Infrastructure.csproj src/journeys/NavigationPlatform.Journeys.Infrastructure/

RUN dotnet restore src/journeys/NavigationPlatform.Journeys.Api/NavigationPlatform.Api.csproj

COPY . .
RUN dotnet publish src/journeys/NavigationPlatform.Journeys.Api/NavigationPlatform.Api.csproj \
    -c Release -o /app/publish 

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NavigationPlatform.Api.dll"]
