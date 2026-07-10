# ─── Stage 1: Build ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY EventsPlatform.slnx ./
COPY src/NotificationWorker/NotificationWorker/NotificationWorker.csproj \
     ./src/NotificationWorker/NotificationWorker/

RUN dotnet restore ./src/NotificationWorker/NotificationWorker/NotificationWorker.csproj

COPY src/NotificationWorker/NotificationWorker/ \
     ./src/NotificationWorker/NotificationWorker/

RUN dotnet publish ./src/NotificationWorker/NotificationWorker/NotificationWorker.csproj \
    --configuration Release \
    --output /app/publish

# ─── Stage 2: Runtime ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN useradd -m appuser
USER appuser

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "NotificationWorker.dll"]