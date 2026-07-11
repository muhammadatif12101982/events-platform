# ADR 004: Outbox Pattern + RabbitMQ for Async Messaging

## Date
2026-07-10

## Status
Accepted

## Context
When an order is created, a notification needs to be sent asynchronously.
The naive approach — write to DB then publish to RabbitMQ — has a fatal flaw:
if RabbitMQ is down or the publish fails, the order is saved but no notification
is ever sent. If we reverse the order, the notification fires but the order
may not save. Either way, we have inconsistency.

## Decision
Use the **Outbox Pattern**:
1. Write the order AND an OutboxMessage in the same DB transaction
2. A background publisher polls the outbox and publishes to RabbitMQ
3. Once published, mark the OutboxMessage as processed

## Reasoning
- Atomic: order + outbox message always succeed or fail together
- Resilient: if RabbitMQ is down, messages accumulate in the outbox
  and are published when it recovers
- Idempotent: MessageId on each message allows consumers to deduplicate

## Messaging infrastructure
- **Broker:** RabbitMQ (Topic exchange)
- **Exchange:** `events-platform`
- **Routing key:** `ordercreated`
- **Consumer queue:** `notification-worker`

## Consequences
- Slight latency: notifications are async, delivered within ~5 seconds
- Outbox table grows — needs periodic cleanup in production
- Consumer must be idempotent — same message may be delivered twice