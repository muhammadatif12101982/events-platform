# ─── Stage 1: Build ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY EventsPlatform.slnx ./
COPY src/Identity/Identity.Server/Identity.Server.csproj ./src/Identity/Identity.Server/

RUN dotnet restore ./src/Identity/Identity.Server/Identity.Server.csproj

COPY src/Identity/Identity.Server/ ./src/Identity/Identity.Server/

RUN dotnet publish ./src/Identity/Identity.Server/Identity.Server.csproj \
    --configuration Release \
    --output /app/publish

# ─── Stage 2: Runtime ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN useradd -m appuser

# Create keys directory owned by appuser before switching user
RUN mkdir -p /app/keys && chown -R appuser:appuser /app/keys

USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5001
EXPOSE 5001

ENTRYPOINT ["dotnet", "Identity.Server.dll"]