# ─── Stage 1: Build ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution and project files first (layer caching optimization)
COPY EventsPlatform.slnx ./
COPY src/Orders/Orders.Api/Orders.Api.csproj ./src/Orders/Orders.Api/

# Restore dependencies (cached unless .csproj changes)
RUN dotnet restore ./src/Orders/Orders.Api/Orders.Api.csproj

# Copy remaining source code
COPY src/Orders/Orders.Api/ ./src/Orders/Orders.Api/

# Publish release build to /app/publish
RUN dotnet publish ./src/Orders/Orders.Api/Orders.Api.csproj \
    --configuration Release \
    --output /app/publish

# ─── Stage 2: Runtime ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN useradd -m appuser
USER appuser

# Copy only the published output from build stage
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Orders.Api.dll"]