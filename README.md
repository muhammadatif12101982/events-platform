# Events Platform

A microservices learning project covering .NET 10, RabbitMQ messaging, 
OpenTelemetry observability, CQRS, and Azure deployment.

## Architecture Overview

Three asymmetric services, each with a distinct responsibility:

| Service | Responsibility | Tech |
|---|---|---|
| **Orders** | Core domain — products, customers, orders | .NET 10, EF Core, Postgres |
| **Gateway/Identity** | Auth, JWT issuance, reverse proxy | .NET 10, YARP, OIDC |
| **Notification Worker** | Async event processing, notifications | .NET 10, RabbitMQ consumer |

## Module Boundaries

### Orders Service owns:
- Product catalog (create, list, price)
- Customer registry
- Order lifecycle (create, status tracking)
- All writes go through the Orders API — no other service touches the Orders database directly

### What Orders Service does NOT own:
- Authentication — that belongs to Gateway/Identity
- Sending notifications — that belongs to the Worker
- Infrastructure concerns (routing, TLS) — that belongs to the Gateway

### The rule:
> Each service owns its data. No service queries another service's database directly.
> Cross-service communication happens via HTTP (sync) or RabbitMQ messages (async).

## Project Structure

```plaintext
src/
├── Orders/
│   └── Orders.Api/
│       ├── Entities/       ← domain model (Customer, Product, Order, OrderItem)
│       ├── Features/       ← one folder per use case (vertical slice pattern)
│       │   ├── Orders/     ← CreateOrder, GetOrder
│       │   ├── Products/   ← CreateProduct, ListProducts
│       │   └── Customers/  ← coming soon
│       └── Migrations/     ← EF Core schema history
├── Gateway/                ← coming Week 2
└── NotificationWorker/     ← coming Week 3
docs/
└── adr/                    ← Architecture Decision Records
```

## Running Locally

### Prerequisites
- Docker Desktop with WSL2 integration enabled
- .NET 10 SDK
- WSL2/Ubuntu

### Start the database
```bash
docker compose up -d
```

### Run the Orders service
```bash
cd src/Orders/Orders.Api
dotnet run
```

### Available endpoints
- `POST /products` — create a product
- `GET /products` — list all products
- `POST /orders` — create an order
- `GET /orders/{id}` — get order by id

## Architecture Decision Records

See [docs/adr/](docs/adr/) for all architectural decisions.

## Status

🚧 Week 1 of 8 — Core domain service in progress.