# Events Platform

![CI](https://github.com/muhammadatif12101982/events-platform/actions/workflows/ci.yml/badge.svg)

A microservices learning project covering .NET 10, Duende IdentityServer, YARP reverse proxy,
RabbitMQ messaging, OpenTelemetry observability, CQRS, and Azure deployment.

## Architecture

```plaintext
                         ┌─────────────────────────────────────────────┐
                         │           Docker Compose Network             │
                         │                                              │
Internet/Client          │  ┌─────────────────┐                        │
     │                   │  │  Identity Server │                        │
     │                   │  │   port 5001      │                        │
     │                   │  │  Duende IS       │                        │
     │                   │  │  JWT Issuance    │                        │
     │                   │  └────────┬─────────┘                        │
     │                   │           │ signing keys                     │
     ▼                   │           ▼                                  │
     │ ──────────────────┼──► ┌─────────────────┐                      │
                         │    │    Gateway       │                      │
                         │    │   port 5000      │                      │
                         │    │  YARP Proxy      │                      │
                         │    │  JWT Validation  │                      │
                         │    └────────┬─────────┘                      │
                         │             │ proxies (authenticated only)   │
                         │             ▼                                 │
                         │    ┌─────────────────┐    ┌──────────────┐  │
                         │    │   Orders API     │───►│  PostgreSQL  │  │
                         │    │   port 8080      │    │  port 5432   │  │
                         │    │  Minimal API     │    └──────────────┘  │
                         │    │  EF Core + CQRS  │                      │
                         │    │  JWT Validation  │                      │
                         │    └─────────────────-┘                      │
                         │                                              │
                         │  Coming: Notification Worker + RabbitMQ     │
                         └─────────────────────────────────────────────┘
```

## Services

| Service | Port | Responsibility |
|---|---|---|
| **Identity Server** | 5001 | OIDC/OAuth2, JWT issuance, client credentials flow |
| **Gateway** | 5000 | YARP reverse proxy, JWT validation, request routing |
| **Orders API** | 8080 | Domain logic, EF Core + Postgres, JWT validation |
| **Postgres** | 5432 | Relational data store |

## Security Model

- All client requests enter through the **Gateway** on port 5000
- Gateway validates JWT before proxying — unauthenticated requests get `401`
- **Orders API also validates JWT independently** — defense in depth, even if Gateway is bypassed
- Tokens issued via **client credentials flow** (machine-to-machine, no user login)
- Scope-based authorization: `orders.read` for GET endpoints, `orders.write` for POST/PUT/DELETE

## Running Locally

### Prerequisites
- Docker Desktop with WSL2 integration
- .NET 10 SDK (for local development without Docker)
- WSL2/Ubuntu

### Start everything
```bash
docker compose up --build
```

### Get a token and call the API
```bash
# Get access token
TOKEN=$(curl -s -X POST http://localhost:5001/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=gateway-client&client_secret=gateway-secret&scope=orders.read%20orders.write" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")

# Call through Gateway (recommended)
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/products
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/orders/1
```

### Available endpoints (via Gateway on port 5000)
| Method | Path | Auth Required | Scope |
|---|---|---|---|
| GET | /products | ✅ Yes | orders.read |
| POST | /products | ✅ Yes | orders.write |
| POST | /orders | ✅ Yes | orders.write |
| GET | /orders/{id} | ✅ Yes | orders.read |

## Project Structure

```plaintext
src/
├── Identity/
│   └── Identity.Server/     ← Duende IdentityServer (OIDC/OAuth2)
├── Gateway/
│   └── Gateway.Api/         ← YARP reverse proxy + JWT validation
├── Orders/
│   └── Orders.Api/
│       ├── Domain/           ← Pure business logic (OrderCalculations)
│       ├── Entities/         ← EF Core domain model
│       ├── Features/         ← Vertical slice handlers
│       └── Migrations/       ← EF Core schema history
tests/
└── Orders.UnitTests/         ← xUnit unit tests
docs/
└── adr/                      ← Architecture Decision Records
```

## Architecture Decision Records

| ADR | Decision |
|---|---|
| [ADR 001](docs/adr/001-vertical-slice-architecture.md) | Vertical slice architecture |
| [ADR 002](docs/adr/002-postgres-primary-database.md) | PostgreSQL as primary database |

## Status

- ✅ Week 1 — Core domain service (Orders API, EF Core, Docker, CI)
- ✅ Week 2 — Auth (IdentityServer, Gateway, YARP, JWT validation)
- ✅ Week 3 — Messaging (RabbitMQ, Outbox pattern, Notification Worker, Testcontainers)
- 🚧 Week 4 — Observability (OpenTelemetry, Jaeger, Prometheus, Grafana)
- ⬜ Week 5 — CQRS read model
- ⬜ Week 6 — IaC (Bicep, Azure Container Apps)
- ⬜ Week 7 — Full Azure deployment + security scanning
- ⬜ Week 8 — Polish + AKS stretch goal