# ADR 007: Secrets Audit — Current State and Key Vault Migration Plan

## Date
2026-07-19

## Status
In Progress (Key Vault migration planned for Week 7)

## Current secrets inventory

### Orders API
| Secret | Current location | Risk | Week 7 action |
|---|---|---|---|
| DB connection string | docker-compose env var | Medium — visible in compose file | Move to Key Vault |
| IdentityServer Authority | appsettings + env var | Low — not a secret, just a URL | Keep in config |
| RabbitMQ credentials | docker-compose env var | Medium — visible in compose file | Move to Key Vault |

### Gateway
| Secret | Current location | Risk | Week 7 action |
|---|---|---|---|
| IdentityServer Authority | docker-compose env var | Low — URL, not a secret | Keep in config |
| YARP destination URLs | docker-compose env var | Low — internal URLs | Keep in config |

### Identity Server
| Secret | Current location | Risk | Week 7 action |
|---|---|---|---|
| Client secret ("gateway-secret") | Config.cs hardcoded | HIGH — in source code | Move to Key Vault |
| Signing key | In-memory (dev) | Low — dev only | Real cert in Key Vault for prod |

### Notification Worker
| Secret | Current location | Risk | Week 7 action |
|---|---|---|---|
| RabbitMQ credentials | docker-compose env var | Medium | Move to Key Vault |

## Critical finding
The IdentityServer client secret `gateway-secret` is **hardcoded in Config.cs**
which is committed to a public GitHub repo. This is a HIGH severity finding.

## Immediate mitigations (before Week 7)
1. The hardcoded secret is development-only and the IdentityServer is not
   publicly accessible — acceptable risk for a learning project
2. For production: NEVER commit secrets to source control
3. All docker-compose secrets should be in a `.env` file excluded from Git

## Week 7 Key Vault migration plan
1. Provision Azure Key Vault via Bicep
2. Store: DB connection string, RabbitMQ credentials, client secrets
3. Configure Container Apps managed identity → Key Vault access
4. Remove all secrets from docker-compose environment variables
5. Replace hardcoded client secret in Config.cs with Key Vault reference

## What stays in config (not secrets)
- Service URLs (IdentityServer authority, YARP destinations)
- Feature flags
- Non-sensitive connection parameters (host names, ports)
- OTLP endpoints