# Modular Monolith Architecture

## Boundaries and Communication Rules

Our backend is built as a modular monolith in ASP.NET Core 10. Boundaries are enforced strictly using NetArchTest to prevent our code from degrading into a "big ball of mud".

### Core Rules

1. **Independent Module Projects:** Each module has its own directory under `backend/modules/` and contains its own Domain, Application, and Infrastructure layers compiled as a single assembly.
2. **Contracts Dependency Only:** A module may reference **only** another module's `Contracts` directory or namespace — never its internal layers (`Domain`, `Application`, `Infrastructure`).
3. **In-Process Interface Communication:** All cross-module communication must go through interfaces defined in `Contracts` or via Domain Events.
4. **No Cross-Module Database Access:** No module can query or join tables belonging to another module directly. All cross-module database entities are isolated.
5. **Shared Database, Isolated Schemas:** All modules share a single PostgreSQL database but maintain separate logical schemas or query filters (e.g. using global EF Core query filters for multi-tenancy isolation).

---

## NetArchTest Enforcement

These architectural boundaries are validated programmatically through unit tests in the `tests/architecture` project. If any boundaries are violated (e.g., direct dependency on a module's Domain layer), the build will fail.
