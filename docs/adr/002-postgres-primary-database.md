# ADR 002: PostgreSQL as Primary Database

## Date
2026-07-02

## Status
Accepted

## Context
The Orders service needs a relational database for storing customers, 
products, orders, and order items — all of which have clear relationships 
and benefit from ACID transaction guarantees (especially order creation, 
which must write the order and its items atomically).

## Decision
Use **PostgreSQL 16** running in Docker locally, and Azure Database for 
PostgreSQL (flexible server) in production (Week 6+).

## Reasoning
- Strong ACID guarantees — critical for order creation (order + items 
  in one transaction)
- First-class EF Core support via Npgsql provider
- Open source, no licensing cost
- Azure managed offering (flexible server) is straightforward to 
  provision via Bicep (Week 6)
- Excellent support for JSON columns if we need semi-structured data later

## Consequences
- All schema changes go through EF Core migrations — no manual SQL scripts
- Local development uses Docker volume for data persistence across 
  container restarts
- Connection strings stored in appsettings.Development.json locally, 
  Azure Key Vault in production (Week 7)

## Alternatives Considered
- **SQL Server** — familiar but adds licensing complexity; Postgres is 
  the better cloud-native choice
- **SQLite** — too limited for production patterns; not representative 
  of real-world workloads