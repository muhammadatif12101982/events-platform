# Events Platform

A microservices learning project covering .NET 10, RabbitMQ messaging, OpenTelemetry observability, CQRS, and Azure deployment.

## Architecture

- **Gateway/Identity** — OIDC auth, JWT issuance, YARP reverse proxy
- **Orders** — core domain service, EF Core + Postgres, CQRS read model, outbox pattern
- **Notification Worker** — RabbitMQ consumer, async event handling

## Status

🚧 Under active development — Week 1 of an 8-week build.

## Running locally

(coming soon — docker-compose instructions)