# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Atipico is a restaurant management system (orders, tables, dishes, accounts/billing) targeting the Bolivian market (`es-BO` currency formatting, Spanish domain names throughout). It's a .NET 10 solution with an ASP.NET Core Web API backend and a Blazor Server frontend, backed by PostgreSQL via EF Core.

## Commands

All commands run from the repo root (`Atipico.slnx`).

```powershell
# Restore + build the whole solution
dotnet build

# Run the API (http://localhost:5197, https://localhost:7273)
dotnet run --project Atipico.Api

# Run the Web frontend (http://localhost:5028, https://localhost:7158) — expects the API running at ApiBaseUrl (defaults to http://localhost:5197)
dotnet run --project Atipico.Web

# Run all tests
dotnet test

# Run a single test project
dotnet test Atipico.Application.Tests

# Run a single test by fully-qualified name
dotnet test --filter "FullyQualifiedName~AuthServiceTests.LoginAsync_ValidCredentials_ReturnsToken"
```

Test projects mirror the `src` projects 1:1: `Atipico.Domain.Tests`, `Atipico.Application.Tests`, `Atipico.Infraestructure.Tests`, `Atipico.Api.Tests`. They use xUnit + Moq (`Moq.EntityFrameworkCore` for mocking `DbSet<T>`/`DbContext` in repository tests).

The API requires `ConnectionStrings:DefaultConnection` and a `Jwt` section (`Key`, `Issuer`, `Audience`, `ExpiryMinutes`) — set in `Atipico.Api/appsettings.Development.json` for local dev. `AppDbContext` also falls back to the `DB_CONNECTION_STRING` env var when no options are configured externally (e.g. for `dotnet ef` tooling).

Database schema/migration SQL lives in `sql/` as hand-written, numbered scripts (not EF Core migrations) — see `sql/002_auth_usuario.sql` for the pattern (wrapped in a transaction, includes backfill logic and comments explaining intent).

## Architecture

Clean/onion architecture with five `src` projects, dependencies flowing inward:

```
Atipico.Api  ──┐
               ├──> Atipico.Application ──> Atipico.Domain
Atipico.Web  ──┘        ^
                         │
         Atipico.Infraestructure (implements Application/Domain interfaces)
```

- **Atipico.Domain** — entities (`Entities/`), enums (`Enums/`), and the persistence-agnostic contracts: `IEntity` (all entities have a `long Id`) and `IRepository<TEntity>` (`Interfaces/Repositories/`). No dependencies on other projects.
- **Atipico.Application** — use-case layer. `IUnitOfWork` (`Common/Interfaces/`) exposes `Repository<TEntity>()` + `SaveChangesAsync()`. `IEntityService<TEntity>`/`EntityService<TEntity>` (`Interfaces/Services/`, `Services/`) is a generic CRUD service built on top of `IUnitOfWork` — this is what most entities use directly with no per-entity override needed. `AuthService`/`IAuthService` handles login (hashing via `IPasswordHasher`, tokens via `IJwtTokenGenerator` — both interfaces defined here, implemented in Infraestructure).
- **Atipico.Infraestructure** — EF Core implementation: `AppDbContext` (Npgsql/PostgreSQL), `Repository<TEntity>` (generic, implements `IRepository<TEntity>`), `UnitOfWork`, per-entity `IEntityTypeConfiguration<T>` classes in `Persistence/Configurations/`, and `Security/` (`BCryptPasswordHasher`, `JwtTokenGenerator`).
- **Atipico.Api** — ASP.NET Core Web API. Every CRUD entity controller extends `EntityControllerBase<TEntity>` (`Controllers/EntityControllerBase.cs`), which wires up `GET`/`GET {id}`/`POST`/`PUT {id}`/`DELETE {id}` against `IEntityService<TEntity>` in one place. Concrete controllers (e.g. `PlatosController`) are typically just a route attribute + constructor — no extra code unless the entity needs custom behavior. Role-based write authorization is done in the base class via `CreateRoles`/`UpdateRoles`/`DeleteRoles` (`string[]`, checked with `User.IsInRole`) — override these virtual properties per controller to loosen/tighten who can mutate vs. the `[Authorize]`-only read default. `AuthController` is the one non-generic controller (`POST api/auth/login`, `[AllowAnonymous]`). JWT bearer auth is configured in `Program.cs` from the `Jwt` config section. OpenAPI/Scalar UI is mapped in Development only.
- **Atipico.Web** — Blazor Server frontend, cookie-authenticated. It does **not** talk to the database directly; it calls the API via a generic `IEntityApiClient<TEntity>`/`EntityApiClient<TEntity>` (`Services/`), which resolves the route for a given entity type from the static map in `ApiRoutes.cs` and does typed JSON HTTP calls. `JwtForwardingHandler` is a `DelegatingHandler` on the `AtipicoApi` named `HttpClient` that pulls the JWT out of the signed-in user's `access_token` claim and forwards it as a `Bearer` header on every API call — this is how the cookie-authenticated Blazor session stays authenticated against the JWT-authenticated API. Login is a plain-old minimal API endpoint (`POST /auth/login` in `Program.cs`, not a Razor page) that calls the API's `/auth/login`, then signs in with a cookie holding the JWT as a claim; logout is `POST /auth/logout`.
  - Pages live under `Components/Pages/<Entity>/` as `Index.razor` (list) + `Edit.razor` (create/update), each `@page`-routed and `[Authorize]`-attributed. List pages use the shared `EntityTable.razor` component (`Components/Shared/`), which takes `Items`, `Columns` (header + value-selector tuples), CRUD URLs, and `WriteRoles` (controls which roles see the create/edit/delete buttons — UI-level only; the API re-checks authorization independently).
  - **Important**: `Program.cs` deliberately does *not* set a global `AddAuthorization` fallback policy requiring authentication — that would break Blazor Server's internal SignalR/`_blazor/*` endpoints, which never go through `AuthorizeRouteView`. Each protected page instead carries its own `[Authorize]` attribute. Keep this in mind when adding new protected routes.

### Adding a new CRUD entity end-to-end

Given the generic-service pattern, a new entity typically touches: `Atipico.Domain/Entities/` (entity implementing `IEntity`) → `Atipico.Domain/Enums/` (if new enums needed) → `Atipico.Infraestructure/Persistence/Configurations/` (EF `IEntityTypeConfiguration`) → `AppDbContext` (add `DbSet`) → a `sql/NNN_*.sql` migration script → `Atipico.Api/Controllers/` (thin controller extending `EntityControllerBase<TEntity>`) → `Atipico.Web/Services/ApiRoutes.cs` (register the route) → `Atipico.Web/Components/Pages/<Entity>/` (`Index.razor` + `Edit.razor`).

### Database business rules and error handling

`sql/script_inicial.sql` is the canonical schema (PostgreSQL 12+) and encodes real business rules the API/UI must respect, not just column shapes:
- **Immutability triggers**: `fn_cuenta_inmutable` forbids any `UPDATE`/`DELETE` on a `cuenta` once it leaves `Abierta`, except a documented transition from `Pagada` to `Anulada` that changes nothing else. `fn_detalle_inmutable` forbids `INSERT`/`UPDATE`/`DELETE` on `detalle_cuenta` once its `cuenta` isn't `Abierta`, and forbids `DELETE` outright — billing lines are voided by annulling the whole cuenta, never removed. `fn_pedido_plato_facturado` blocks setting a `PedidoPlato` to `Anulado` once it has a `DetalleCuenta` (annul the cuenta instead). Controllers/pages for `Cuenta` and `PedidoPlato` must mirror these state machines in the UI (see `Cuentas/Edit.razor`, `PedidoPlatos/Edit.razor`) rather than let requests hit the trigger blind.
- **Check constraints** duplicate the C# enums as `CHECK (col IN (...))` with hardcoded UPPER_SNAKE_CASE values (matching `UpperSnakeCaseEnumConverter`) — adding an enum value in C# without a matching SQL migration will fail silently until exercised in production. There is no single source of truth; the two must be changed together by hand.
- **Views** `v_pedido_plato_sin_cobrar` (served/pending dishes not yet billed) and `v_cuenta_descuadrada` (cuentas whose `monto` doesn't match the sum of their detail) are mapped as keyless EF entities (`PedidoPlatoSinCobrar`, `CuentaDescuadrada`, `.HasNoKey()` configs) and exposed read-only via `ReportesController` (`GET api/reportes/pedido-platos-sin-cobrar`, `GET api/reportes/cuentas-descuadradas`) — deliberately **not** through `EntityControllerBase<T>`, since Create/Update/Delete don't make sense (and would throw) on a keyless view. `Cuentas/Index.razor` → new `Reportes/CuentasDescuadradas.razor` page (Admin/Cajero) surfaces the reconciliation view; `PedidosController.Update` checks `v_pedido_plato_sin_cobrar` before allowing a transition to `Cerrado`, rejecting with a 409 if unbilled dishes remain.
- **`app_restaurante` DB role**: the app connects as this low-privilege role (not the `postgres` superuser), matching the intent of BLOQUE 2 in `script_inicial.sql` (`GRANT SELECT, INSERT, UPDATE`, no `DELETE`/`TRUNCATE`). Since the schema's own stated philosophy is "anular, no borrar" as a whole-system rule (not just for billing), this means **every** `DELETE` endpoint now always fails — regardless of whether the specific row has FK/trigger obstructions — with a translated 409 (see `PostgresErrorCodes.InsufficientPrivilege` in `TryTranslateDbError`) instead of silently succeeding. All 9 `Index.razor` pages with a delete button now catch `ApiException`/`HttpRequestException` around `HandleDelete` and show `_error` instead of crashing the Blazor circuit. **BLOQUE 2 of the script (role creation + grants) was not part of the original schema run and had to be executed by hand**; if the role is ever dropped/recreated, rerun it and reset the password via `ALTER ROLE app_restaurante WITH PASSWORD '<clave>'`, then update `ConnectionStrings:DefaultConnection` (dev) or the `DB_CONNECTION_STRING` env var (any real deployment — `appsettings.json` has no connection string configured, only the Development file does).

**Error translation**: `EntityControllerBase.TryTranslateDbError` catches `DbUpdateException`, unwraps the inner `PostgresException`, and turns unique/check/FK/insufficient-privilege violations plus trigger-raised `P0001` messages into a 400/409 with `{"message": "..."}` in Spanish — instead of a raw 500 with a stack trace. Any controller that overrides `Create`/`Update` directly (bypassing the base implementation — see `UsuariosController`, `PedidosController`, `PedidoPlatosController`, `CuentasController`) must wrap its own `_service.AddAsync`/`UpdateAsync` call in the same `try { } catch (DbUpdateException ex) { var r = TryTranslateDbError(ex); if (r is not null) return r; throw; }` pattern to get this. On the Blazor side, `Atipico.Web/Services/ApiException.cs` + `EntityApiClient<T>` read that `message` field and throw/surface it; Edit pages catch `ApiException` (friendly message) separately from `HttpRequestException` (real connectivity failure, generic message).

**Transition timestamps** (`ServidoEn`, `CerradoEn`, `PagadoEn`, `AnuladoEn`) are never taken from the client — Npgsql rejects non-UTC `DateTimeOffset` writes to `timestamptz`, and a browser-built `InputDate` carries the local offset. `PedidosController`/`PedidoPlatosController`/`CuentasController` override `Update` to fetch-and-mutate the tracked entity and stamp `DateTimeOffset.UtcNow` themselves when they detect the relevant state transition; the corresponding Razor forms only show these fields read-only, never as editable inputs.

### Roles

`RolUsuario` enum (`Atipico.Domain/Enums/RolUsuario.cs`): `Mesero`, `Cajero`, `Cocinero`, `Admin`. Used both for API authorization (`CreateRoles`/`UpdateRoles`/`DeleteRoles` on controllers) and Web UI gating (`WriteRoles` on `EntityTable`).
