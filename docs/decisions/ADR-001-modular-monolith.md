# ADR-001 — Modular Monolith

* **Status:** Accepted
* **Date:** 2026-08-22

## Context

We need an architecture that supports modularity and clear domain boundaries to support a multi-year product roadmap. However, we are a small team, and microservices would introduce excessive operational overhead, network latency, deployment complexity, and distributed transaction challenges.

## Decision

We will build the backend as an ASP.NET Core **Modular Monolith**. 

- Each module represents a distinct bounded context.
- Each module resides in its own folder under `modules/` and compiles into a single assembly.
- Cross-module calls must ONLY reference the target module's `Contracts` project/namespace.
- Direct references to another module's `Domain`, `Application`, or `Infrastructure` are prohibited.
- These rules are programmatically validated and enforced using `NetArchTest` inside the architecture tests suite.

## Consequences

- Modularity is achieved with very low operational overhead.
- Simplifies deployment (we deploy a single package).
- Cross-module communication is in-process and fast.
- Keeps options open: if a specific module needs to be scaled independently in the future, it can be easily extracted into a microservice since it has clean boundaries and no direct dependencies.
