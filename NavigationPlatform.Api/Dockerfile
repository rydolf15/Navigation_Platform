# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NavigationPlatform.Api/NavigationPlatform.Api.csproj NavigationPlatform.Api/
COPY NavigationPlatform.Application/NavigationPlatform.Application.csproj NavigationPlatform.Application/
COPY NavigationPlatform.Domain/NavigationPlatform.Domain.csproj NavigationPlatform.Domain/
COPY NavigationPlatform.Infrastructure/NavigationPlatform.Infrastructure.csproj NavigationPlatform.Infrastructure/

RUN dotnet restore NavigationPlatform.Api/NavigationPlatform.Api.csproj

COPY . .
RUN dotnet publish NavigationPlatform.Api/NavigationPlatform.Api.csproj \
    -c Release -o /app/publish 

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NavigationPlatform.Api.dll"]
