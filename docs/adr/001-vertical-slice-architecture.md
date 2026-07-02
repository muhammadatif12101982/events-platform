# ADR 001: Vertical Slice Architecture for Feature Organization

## Date
2026-07-02

## Status
Accepted

## Context
When scaffolding the Orders service, we needed to decide how to organize 
feature code within the project. The two main options were:

1. **Traditional layered architecture** — separate folders for Controllers, 
   Services, and Repositories, grouping files by technical role
2. **Vertical slice architecture** — one folder per use case, grouping all 
   code for a feature together regardless of technical role

## Decision
We chose **vertical slice architecture**, organizing code by feature under 
a `Features/` folder. Each use case (e.g. `CreateOrder`, `ListProducts`) 
lives in its own file containing the request, response, and handler.

## Reasoning

### Why vertical slices:
- A single feature change touches one file, not three or four across 
  different folders
- Each slice is independently readable — no mental context-switching 
  between layers to understand one use case
- Scales naturally as the service grows — adding a feature means adding 
  a file, not modifying existing layer files
- Aligns with how we think about requirements ("create an order") rather 
  than technical roles ("the order service calls the order repository")

### Why not traditional layering:
- Layering groups files by what they *are* technically, not what they *do* 
  for the business
- A change to one feature (e.g. "add tax calculation to CreateOrder") 
  requires touching Controller + Service + Repository — three files, 
  three PRs to review, three places to look
- Leads to "god services" (OrderService with 20 methods) that are hard 
  to reason about

## Consequences

### Positive:
- New team members can understand a feature by reading one file
- Easy to move a feature to a different service later — it's already 
  self-contained
- Natural fit for the minimal API pattern in .NET 10

### Negative:
- Some code duplication across slices (e.g. similar validation logic 
  may appear in multiple handlers) — acceptable tradeoff, extract to 
  shared helpers only when duplication becomes painful
- Less familiar to developers who only know traditional MVC layering

## Alternatives Considered
- **MediatR + CQRS handlers** — adds a dependency and indirection for 
  little gain at this scale; can be introduced later if the codebase 
  grows significantly
- **Traditional Controllers** — rejected, see reasoning above