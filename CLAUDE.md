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

# Run both services locally in Docker (copy .env.example to .env first)
docker compose up
```

Test projects mirror the `src` projects 1:1: `Atipico.Domain.Tests`, `Atipico.Application.Tests`, `Atipico.Infraestructure.Tests`, `Atipico.Api.Tests`. They use xUnit + Moq (`Moq.EntityFrameworkCore` for mocking `DbSet<T>`/`DbContext` in repository tests).

The API requires `ConnectionStrings:DefaultConnection` and a `Jwt` section (`Key`, `Issuer`, `Audience`, `ExpiryMinutes`). The `Jwt` section is set directly in `Atipico.Api/appsettings.Development.json`. `ConnectionStrings:DefaultConnection` is deliberately left empty there — set it via .NET User Secrets instead (`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..." --project Atipico.Api`), so a real DB credential is never committed. `AppDbContext` also falls back to the `DB_CONNECTION_STRING` env var when no options are configured externally (e.g. for `dotnet ef` tooling).

Database schema/migration SQL lives in `sql/` as hand-written, numbered scripts (not EF Core migrations) — see `sql/002_auth_usuario.sql` for the pattern (wrapped in a transaction, includes backfill logic and comments explaining intent). `sql/schema_completo.sql` is a *generated* snapshot (via `pg_dump --schema-only` against the real DB, not hand-written) equivalent to running `script_inicial.sql` + all numbered migrations in order — a one-file shortcut for standing up a new environment, not a replacement for the numbered migration history, which stays canonical.

Both `Atipico.Api/Dockerfile` and `Atipico.Web/Dockerfile` deliberately restore against the full source tree (not `--no-restore`) after the initial COPY-csproj-only restore — see the comment in `Atipico.Web/Dockerfile`. Doing the `--no-restore` "optimization" silently drops Blazor's framework static web assets (`_framework/blazor.web.js` etc.) from the published Web image, so the app 404s at runtime with no build-time error. `docker-compose.yml` runs both containers locally from a gitignored `.env` (see `.env.example`); `render.yaml` deploys the same two Dockerfiles as a Render Blueprint (README.md has the env var table).

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
  - `NavMenu.razor` nests `AuthorizeView Roles="..."` blocks per section (and per link within a section) to hide whole nav groups a role can't use — e.g. only `Admin` sees "Personal"/"Carta"/"Mesas"; `Pedido <-> Mesa` and `Pedido <-> Plato` links are `Admin`-only even though the parent "Pedidos" section is visible to `Mesero`/`Cocinero` too. This is UI-only convenience (the API re-checks via `CreateRoles`/`UpdateRoles`/`DeleteRoles`), but new nav links should follow the same nested-`AuthorizeView` pattern rather than relying on the page's own `[Authorize(Roles=...)]` alone. `Home.razor` similarly branches its dashboard content by role (a `Mesero` gets a reduced view).
  - New-record forms that have a "current actor" field default it to the logged-in user rather than an arbitrary list entry: `Pedidos/Edit.razor` reads `ClaimTypes.NameIdentifier` off the cascading `AuthenticationState` to default `IdMesero` to the current user on create (falling back to the first mesero only if that claim is missing), and hides the `Estado`/`Mesero` inputs entirely until `Id is not null` (i.e. only editable once the pedido exists, not at creation).

### Adding a new CRUD entity end-to-end

Given the generic-service pattern, a new entity typically touches: `Atipico.Domain/Entities/` (entity implementing `IEntity`) → `Atipico.Domain/Enums/` (if new enums needed) → `Atipico.Infraestructure/Persistence/Configurations/` (EF `IEntityTypeConfiguration`) → `AppDbContext` (add `DbSet`) → a `sql/NNN_*.sql` migration script → `Atipico.Api/Controllers/` (thin controller extending `EntityControllerBase<TEntity>`) → `Atipico.Web/Services/ApiRoutes.cs` (register the route) → `Atipico.Web/Components/Pages/<Entity>/` (`Index.razor` + `Edit.razor`).

### Database business rules and error handling

`sql/script_inicial.sql` is the canonical schema (PostgreSQL 12+) and encodes real business rules the API/UI must respect, not just column shapes:
- **Immutability triggers**: `fn_cuenta_inmutable` forbids any `UPDATE`/`DELETE` on a `cuenta` once it leaves `Abierta`, except a documented transition from `Pagada` to `Anulada` that changes nothing else. `fn_detalle_inmutable` forbids `INSERT`/`UPDATE`/`DELETE` on `detalle_cuenta` once its `cuenta` isn't `Abierta`, and forbids `DELETE` outright — billing lines are voided by annulling the whole cuenta, never removed. `fn_pedido_plato_facturado` blocks setting a `PedidoPlato` to `Anulado` once it has a `DetalleCuenta` (annul the cuenta instead). Controllers/pages for `Cuenta` and `PedidoPlato` must mirror these state machines in the UI (see `Cuentas/Edit.razor`, `PedidoPlatos/Edit.razor`) rather than let requests hit the trigger blind.
- **Check constraints** duplicate the C# enums as `CHECK (col IN (...))` with hardcoded UPPER_SNAKE_CASE values (matching `UpperSnakeCaseEnumConverter`) — adding an enum value in C# without a matching SQL migration will fail silently until exercised in production. There is no single source of truth; the two must be changed together by hand.
- **Views** `v_pedido_plato_sin_cobrar` (served/pending dishes not yet billed) and `v_cuenta_descuadrada` (cuentas whose `monto` doesn't match the sum of their detail) are mapped as keyless EF entities (`PedidoPlatoSinCobrar`, `CuentaDescuadrada`, `.HasNoKey()` configs) and exposed read-only via `ReportesController` (`GET api/reportes/pedido-platos-sin-cobrar`, `GET api/reportes/cuentas-descuadradas`) — deliberately **not** through `EntityControllerBase<T>`, since Create/Update/Delete don't make sense (and would throw) on a keyless view. `Cuentas/Index.razor` → new `Reportes/CuentasDescuadradas.razor` page (Admin/Cajero) surfaces the reconciliation view; `PedidosController.Update` checks `v_pedido_plato_sin_cobrar` before allowing a transition to `Cerrado`, rejecting with a 409 if unbilled dishes remain.
- **Unique active comensal per pedido**: `sql/003_pedido_comensal_unico.sql` adds a partial unique index (`uk_pedido_comensal_activo`) on `pedido.comensal` filtered to `estado IN ('ABIERTO', 'EN_PREPARACION')` — two active pedidos can't share a diner name (NULL comensals and non-active pedidos are exempt). Mirrored in `PedidoConfiguration.cs` via `HasIndex(...).HasFilter(...)` and translated to a Spanish message in `EntityControllerBase.TryTranslateDbError`.
- **`app_restaurante` DB role**: the app connects as this low-privilege role (not the `postgres` superuser), matching the intent of BLOQUE 2 in `script_inicial.sql` (`GRANT SELECT, INSERT, UPDATE`, no `DELETE`/`TRUNCATE`). Since the schema's own stated philosophy is "anular, no borrar" as a whole-system rule (not just for billing), this means **every** `DELETE` endpoint now always fails — regardless of whether the specific row has FK/trigger obstructions — with a translated 409 (see `PostgresErrorCodes.InsufficientPrivilege` in `TryTranslateDbError`) instead of silently succeeding. All 9 `Index.razor` pages with a delete button now catch `ApiException`/`HttpRequestException` around `HandleDelete` and show `_error` instead of crashing the Blazor circuit. **BLOQUE 2 of the script (role creation + grants) was not part of the original schema run and had to be executed by hand**; if the role is ever dropped/recreated, rerun it and reset the password via `ALTER ROLE app_restaurante WITH PASSWORD '<clave>'`, then update `ConnectionStrings:DefaultConnection` (dev) or the `DB_CONNECTION_STRING` env var (any real deployment — `appsettings.json` has no connection string configured, only the Development file does).

**Error translation**: `EntityControllerBase.TryTranslateDbError` catches `DbUpdateException`, unwraps the inner `PostgresException`, and turns unique/check/FK/insufficient-privilege violations plus trigger-raised `P0001` messages into a 400/409 with `{"message": "..."}` in Spanish — instead of a raw 500 with a stack trace. Any controller that overrides `Create`/`Update` directly (bypassing the base implementation — see `UsuariosController`, `PedidosController`, `PedidoPlatosController`, `CuentasController`) must wrap its own `_service.AddAsync`/`UpdateAsync` call in the same `try { } catch (DbUpdateException ex) { var r = TryTranslateDbError(ex); if (r is not null) return r; throw; }` pattern to get this. On the Blazor side, `Atipico.Web/Services/ApiException.cs` + `EntityApiClient<T>` read that `message` field and throw/surface it; Edit pages catch `ApiException` (friendly message) separately from `HttpRequestException` (real connectivity failure, generic message).

**Transition timestamps** (`ServidoEn`, `CerradoEn`, `PagadoEn`, `AnuladoEn`) are never taken from the client — Npgsql rejects non-UTC `DateTimeOffset` writes to `timestamptz`, and a browser-built `InputDate` carries the local offset. `PedidosController`/`PedidoPlatosController`/`CuentasController` override `Update` to fetch-and-mutate the tracked entity and stamp `DateTimeOffset.UtcNow` themselves when they detect the relevant state transition; the corresponding Razor forms only show these fields read-only, never as editable inputs.

Those UTC timestamps are stored correctly but must not be displayed raw: Blazor Server renders on the server, so `DateTimeOffset.ToLocalTime()` resolves to the *server's* OS timezone (UTC in a Docker container), not the browser's. `Atipico.Web/DateTimeOffsetExtensions.cs` adds `ToBoliviaTime()` (a fixed UTC-4 offset — Bolivia has no DST, so this avoids depending on the container having the IANA timezone database installed) and every Razor page that renders a stored timestamp (`Cuentas/Edit.razor`, `PedidoPlatos/Edit.razor`, `Pedidos/Edit.razor`, `Pedidos/Index.razor`, `Reportes/CuentasDescuadradas.razor`) calls it before formatting. New pages displaying a `DateTimeOffset` from the API should follow the same pattern rather than calling `ToLocalTime()`.

### Roles

`RolUsuario` enum (`Atipico.Domain/Enums/RolUsuario.cs`): `Mesero`, `Cajero`, `Cocinero`, `Admin`. Used both for API authorization (`CreateRoles`/`UpdateRoles`/`DeleteRoles` on controllers) and Web UI gating (`WriteRoles` on `EntityTable`).

### Global progress bar

`Atipico.Web/Components/Shared/BarraProgreso.razor` renders a thin indeterminate bar at the top of the viewport whenever an API call is in flight. It sits in `MainLayout.razor`, so it covers the whole app; **individual pages need no code for it**.

The wiring is a decorator, not an HTTP handler, and that choice is load-bearing: `IHttpClientFactory` pools and reuses `DelegatingHandler` instances outside the Blazor circuit's DI scope, so a scoped counter injected there would capture the wrong circuit and one user's bar would light up for another user's request. Instead `EntityApiClient<T>` and `ComprobanteApiClient` are registered by their concrete type, and the interfaces resolve to `EntityApiClientConProgreso<T>` / `ComprobanteApiClientConProgreso`, which report to the scoped `EstadoOperaciones` (scoped == per circuit in Blazor Server). The counter increments on start and decrements in a `finally`, so a failed call can't leave the bar stuck; overlapping calls only clear it when the last one finishes.

**The consequence for new code**: anything reached through `IEntityApiClient<T>` or `IComprobanteApiClient` gets the bar for free, but a raw `HttpClientFactory.CreateClient("AtipicoApi")` call bypasses it silently. There is exactly one such call today — the `metodoPago` query-string PUT in `Pedidos/Edit.razor`, which can't go through the generic client — and it wraps itself in `EstadoOperaciones.SeguirAsync` by hand. Any new raw-`HttpClient` call must do the same, as must long non-HTTP waits (`PrepararComprobantesAsync` wraps its `OpenReadStream` copy, since a phone photo crossing the SignalR circuit is a real wait with no request behind it).

The bar is deliberately **indeterminate**: neither the API nor R2 reports progress, so a percentage would be fabricated. It also honours `prefers-reduced-motion` by holding a static band instead of animating.

**Blocking overlay.** `EstadoOperaciones` also exposes `BloquearAsync(accion, mensaje)`, rendered by `Bloqueo.razor` (also in `MainLayout`) as a full-viewport overlay with a spinner and a message. It uses a **separate counter** from the bar on purpose: the bar accompanies every call including a page's background loads, and blocking the screen for each of those would be unbearable. Blocking is opt-in and reserved for user-triggered actions that admit nothing else while they run — today the four buttons in `Pedidos/Edit.razor`, wrapped by its local `EjecutarAsync` helper.

Both counters release in a `finally`, so a failed action can never leave the screen blocked or the bar stuck. Buttons are *also* disabled during the action, which is not redundant: it closes the window between the click and the overlay rendering, and on "En Preparación" a double click would try to bill twice (the server already rejects it — `PedidosController` only bills on the `Abierto -> EnPreparacion` transition — but the UI shouldn't offer it).

### Sorting in `EntityTable`

`EntityTable` renders clickable, sortable headers **only** when the page hooks its `OnSort`
callback (plus `SortHeader` / `SortDescending`). Pages that don't hook it render exactly as
before. The grid never sorts anything itself, and that is deliberate: a column is declared as
`(Header, Func<TEntity, object?> Value)` where `Value` returns the **already formatted text**
— currency via `"C"`, dates via `"g"`/`"d"`, `"Sí"`/`"No"` for booleans. Sorting on those
strings gives plausible-looking but wrong results (`Bs 1.000,00` before `Bs 9,50`; dates
ordered by day-of-month). Only the page knows the real value behind each column, so the
ordering lives there.

`Pedidos/Index.razor` is the reference implementation. Two things worth copying: `Creado` is
ordered **by day, not by instant** (ordering by the full timestamp would make every secondary
criterion unreachable, since each row has its own microsecond — and the page's own date
filter already treats `Creado` as a day); and the non-selected columns stay on as
tie-breakers in the default order, so sorting by `Estado` still shows the most recent first
within each state instead of an arbitrary order. `Id` closes the chain to keep the order
stable across re-renders.

### Mobile layout

**`641px` is the single mobile breakpoint** for the whole app, because it is the width at
which `MainLayout.razor.css` decides whether the sidebar exists. Everything else keys off
the same number (`@media (max-width: 640.98px)`), so "there is a bottom tab bar" and "there
is no sidebar" are always the same condition. CSS can't read a custom property in a media
query, so the number is repeated by hand in `app.css`, `MainLayout.razor.css`,
`EntityTable.razor.css` and `BarraInferior.razor.css` — a new mobile rule belongs at that
same breakpoint, not a Bootstrap one (`sm`/`md` don't line up with it).

- **`EntityTable` renders its rows twice** — once as the `<table>`, once as
  `.entity-cards` — and CSS shows exactly one. This is deliberate, not an oversight: Blazor
  Server renders on the server and cannot know the viewport, so a single-markup solution
  would need a JS-interop breakpoint probe and would flash the wrong layout on first paint.
  The cost is a doubled DOM per row; it is paid because the table needs horizontal dragging
  on a phone to reach the last column, which is usually the state. In the card, the title is
  **the first column whose header isn't `Id`** (`IndiceTitulo`) — most Index pages open with
  `Id`, and a list of primary keys is unreadable; the `Id` drops into the context line
  prefixed with `#`. Pass `TituloHeader` to override. The sortable `<th>` goes away with the
  table, so the card list grows its own "Ordenar por" select plus a direction button — same
  `OnSort` contract, the page still owns the ordering.
- **Navigation is one model, two drawings.** `Atipico.Web/Services/Navegacion.cs` holds the
  sections and links; `NavMenu.razor` draws them as the desktop sidebar and
  `BarraInferior.razor` draws them as the mobile tab bar plus a "Más" sheet. A section's
  `RolesCsv` is **derived** from the union of its links' roles rather than declared: the two
  used to be separate lists and drifted, so a `Cocinero` saw a "Pedidos" heading with
  nothing under it. Deriving it makes that state unwritable. Adding a link means editing
  `Navegacion.cs` only — never one of the two components. This is UI gating only; the API
  re-checks every action.
- **Touch targets.** `app.css` forces `min-height: 44px` and `display: inline-flex` on
  `.btn`/`.btn-sm` below the breakpoint (`min-height` alone on an inline-block leaves the
  label stuck to the top). Inputs go to `font-size: 16px` there too — iOS zooms into any
  field under 16px and never zooms back out; the visible size is held by `min-height`, so
  nothing looks bigger.
- **Fixed bottom chrome.** The tab bar is `z-index: 1030` and the "Más" sheet `1035`/`1036`,
  which is why `#blazor-error-ui` was raised from `1000` to `1040` — it is also pinned to
  the bottom and was being covered exactly when it needed reading. `Ayuda`'s tooltip
  (`1080`), `BarraProgreso` (`2000`) and `Bloqueo` (`2100`) stay above all of it. `.content`
  reserves `--atipico-tabbar` of bottom padding so the last row of any list stays reachable.
- **The panel does one API call, not nine.** `Home.razor` used to load all nine entity
  lists in sequence purely to put a count on each card. It now loads `Pedido` only and shows
  the orders in progress; the shortcut cards carry no counts. When adding to the dashboard,
  do not reintroduce a count that costs a full `GetAllAsync()` — there is no count endpoint.

Enum display strings live in `Atipico.Web/*Extensions.cs` (`TipoPedidoExtensions`,
`EstadoPedidoExtensions`), never inline in a page: `EnPreparacion` reads badly in a status
pill, and two pages formatting the same enum by hand is how they drift. `EstadoPedidoExtensions.ClasePill()`
also maps a state to the `.estado-pill` modifier, so the same state looks the same in the
grid and on the panel.
