FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/gateway/NavigationPlatform.Gateway/NavigationPlatform.Gateway.csproj src/gateway/NavigationPlatform.Gateway/
RUN dotnet restore src/gateway/NavigationPlatform.Gateway/NavigationPlatform.Gateway.csproj

COPY . .
RUN dotnet publish src/gateway/NavigationPlatform.Gateway/NavigationPlatform.Gateway.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NavigationPlatform.Gateway.dll"]

