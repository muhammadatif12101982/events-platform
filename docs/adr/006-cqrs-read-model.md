# ADR 006: CQRS Read Model for Order Summaries

## Date
2026-07-19

## Status
Accepted

## Context
The Orders API has two different access patterns that are fundamentally
at odds with each other:

**Write pattern** — normalized, consistent, transactional:
- Create order with items → requires referential integrity
- Update order status → requires consistent state
- EF Core with full entity graph

**Read pattern** — denormalized, fast, display-oriented:
- Show order summary → needs product names, totals, item counts
- List orders → needs pre-joined data without N+1 queries
- Current implementation requires JOINs across 3 tables per request

## Decision
Implement a **CQRS read model** using an `OrderSummaries` table:
- Separate read table in the same Postgres database
- Kept in sync by an `OrderProjection` background service
- Projection consumes `OrderCreatedEvent` from RabbitMQ
- Read endpoint (`GET /orders/{id}/summary`) queries only `OrderSummaries`

## Reasoning
- Read queries become a single-table lookup — no JOINs
- Write and read schemas can evolve independently
- Projection is idempotent — safe to replay events
- Reuses existing RabbitMQ infrastructure (same exchange, new queue)
- Eventual consistency window is ~2 seconds — acceptable for order history

## Consequences

### Positive:
- Read endpoint is faster and simpler (no JOINs, no EF includes)
- ItemsSummary pre-computed ("Laptop x2, Keyboard x1") — no client-side formatting
- Projection lag is measurable as a Prometheus metric

### Negative:
- Eventual consistency — summary may be stale for ~2-5 seconds after creation
- Two sources of truth — bugs in projection can cause read/write divergence
- Projection must handle duplicates idempotently (handled via HashSet deduplication)

## Alternatives Considered
- **Separate read database** — rejected, unnecessary complexity at this scale
- **Materialized view** — simpler but no event-driven update mechanism
- **Cache layer (Redis)** — adds infrastructure without teaching the pattern

## Consistency window
Measured at ~2 seconds in development. Factors affecting this:
- OutboxPublisher poll interval (5 seconds)
- RabbitMQ delivery latency (<100ms)
- Projection processing time (<50ms)