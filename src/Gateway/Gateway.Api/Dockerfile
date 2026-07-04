# ─── Stage 1: Build ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY EventsPlatform.slnx ./
COPY src/Gateway/Gateway.Api/Gateway.Api.csproj ./src/Gateway/Gateway.Api/

RUN dotnet restore ./src/Gateway/Gateway.Api/Gateway.Api.csproj

COPY src/Gateway/Gateway.Api/ ./src/Gateway/Gateway.Api/

RUN dotnet publish ./src/Gateway/Gateway.Api/Gateway.Api.csproj \
    --configuration Release \
    --output /app/publish

# ─── Stage 2: Runtime ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN useradd -m appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "Gateway.Api.dll"]