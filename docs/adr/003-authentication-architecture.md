# ADR 003: Authentication Architecture — Duende IdentityServer + YARP Gateway

## Date
2026-07-04

## Status
Accepted

## Context
The Orders API needed protection. Three options were considered:
1. Put auth directly in the Orders API only
2. Put auth only in the Gateway and trust it downstream
3. Gateway validates tokens AND each service validates independently

## Decision
Option 3 — **defense in depth**. Both the Gateway and Orders API validate
JWTs independently. The Gateway acts as the first line of defense; Orders API
is the second.

## Reasoning
- A misconfigured Gateway or a direct network path bypassing it should not
  expose the Orders API
- Each service owning its own auth boundary is the correct microservices pattern
- The cost (one extra `AddAuthentication` call per service) is negligible
  compared to the security gain

## Token flow
1. Client calls `POST /connect/token` on IdentityServer with client credentials
2. IdentityServer issues a signed JWT with `aud=orders-api` and requested scopes
3. Client includes `Authorization: Bearer <token>` on all requests
4. Gateway validates the token — rejects with 401 if invalid
5. Gateway proxies to Orders API — which also validates the token
6. Orders API checks scope claim — rejects with 403 if insufficient scope

## Consequences
- Tokens expire after 1 hour — clients must re-authenticate
- In production, client secrets move to Azure Key Vault (Week 7)
- IdentityServer signing key is in-memory (development) — changes on restart
  invalidating all existing tokens; this is acceptable in development