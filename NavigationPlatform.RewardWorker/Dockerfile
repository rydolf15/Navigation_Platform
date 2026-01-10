FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NavigationPlatform.RewardWorker/NavigationPlatform.RewardWorker.csproj NavigationPlatform.RewardWorker/
COPY NavigationPlatform.Application/NavigationPlatform.Application.csproj NavigationPlatform.Application/
COPY NavigationPlatform.Domain/NavigationPlatform.Domain.csproj NavigationPlatform.Domain/
COPY NavigationPlatform.Infrastructure/NavigationPlatform.Infrastructure.csproj NavigationPlatform.Infrastructure/

RUN dotnet restore NavigationPlatform.RewardWorker/NavigationPlatform.RewardWorker.csproj

COPY . .
RUN dotnet publish NavigationPlatform.RewardWorker/NavigationPlatform.RewardWorker.csproj \
    -c Release -o /app/publish 

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NavigationPlatform.RewardWorker.dll"]
