FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy everything
COPY . ./

# Restore as distinct layers
RUN dotnet restore "AssetHub.Api/AssetHub.Api.csproj"

# Build and publish a release
RUN dotnet publish "AssetHub.Api/AssetHub.Api.csproj" -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AssetHub.Api.dll"]
