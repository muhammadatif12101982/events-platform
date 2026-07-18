# ADR 005: OpenTelemetry for Distributed Observability

## Date
2026-07-18

## Status
Accepted

## Context
With three services communicating synchronously (HTTP) and asynchronously
(RabbitMQ), debugging issues by reading individual service logs is slow
and error-prone. A single user action triggers code across Gateway,
Orders API, Postgres, RabbitMQ, and Notification Worker — impossible to
follow manually.

## Decision
Instrument all services with **OpenTelemetry** using:
- **Traces** → Jaeger via OTLP gRPC (port 4317)
- **Metrics** → Prometheus scraping /metrics endpoints every 15s
- **Dashboards** → Grafana, provisioned via YAML config committed to repo

## Instrumentation coverage

| Service | Traces | Metrics |
|---|---|---|
| Gateway | ASP.NET Core + HTTP client (YARP) | Yes |
| Orders API | ASP.NET Core + HTTP client + EF Core + custom (outbox.publish) | Yes |
| Notification Worker | Custom (notification.process) | No |

## RabbitMQ trace propagation
Trace context crosses the async RabbitMQ boundary via W3C TraceContext
injected into AMQP message headers. This means the Notification Worker's
processing span appears as a child of the publisher's span — one connected
trace across the async boundary.

## Key outcomes
- Single request traceable: Gateway → Orders API → Postgres (SQL visible)
- Async chain traceable: outbox.publish → notification.process (across RabbitMQ)
- Prometheus metrics scraped from both APIs
- Grafana dashboard exported as JSON in repo — reproducible in any environment

## Consequences
- OTLP endpoint configured per-environment via environment variables
- Jaeger is development-only — production uses Azure Monitor (Week 7)
- Adding a new service needs 3 packages + ~15 lines of config in Program.cs