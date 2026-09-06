# Rol Delivery: solo ve pedidos ya preparados

Especificación funcional y técnica para SCRUM-13 (Jira, proyecto `atipico`): "Crear delivery
como un rol y configurar sus permisos". Documento de referencia previo a la implementación:
recoge las decisiones tomadas y su porqué, para no volver a discutirlas al escribir el código.

- **Estado:** **propuesto**. Sin código todavía.
- **Origen:** [SCRUM-13](https://caverop.atlassian.net/browse/SCRUM-13), tipo Task. Descripción
  completa del ticket: *"Se necesita agregar un nuevo rol, con solamente una restriccion, este
  solo puede ver pedido ya preparados."*
- **Alcance:** un rol `Delivery` que, en la grilla de pedidos, solo ve los pedidos con
  `Estado = Servido` y `Tipo = Delivery` — los que están listos para repartir. Sin permisos de
  escritura en ningún lado (no crea, no edita, no anula nada).
- **Fuera de alcance, confirmado con el usuario durante el análisis (§8, Bitácora):**
  - Ver el **detalle** de un pedido (`Pedidos/Edit.razor`, con la dirección de entrega) queda
    para un ticket aparte. Este ticket cubre únicamente la grilla.
  - No se restringe el acceso a ninguna otra entidad (`PedidoPlato`, `DetalleCuenta`, `Cuenta`,
    `Mesa`, `Plato`, `Usuario`, ...): la única restricción que pide el ticket es sobre Pedido,
    y no se inventan restricciones adicionales que nadie pidió (§6 detalla el hueco que esto
    deja, a sabiendas).
  - No se agrega una acción para que Delivery marque un pedido como entregado/cerrado. Es
    puramente de lectura.

---

## 1. Contexto: por qué esto es más que "agregar un valor a un enum"

Agregar un rol nuevo (`RolUsuario`) es lo fácil y ya tiene un patrón clarísimo en el repo
(`specs/tipo-pedido.md` lo hizo para `TipoPedido`). Lo que hace a este ticket distinto es la
restricción: **es la primera vez que un rol necesita ver *menos* filas de una entidad, no
menos botones.**

Hoy la autorización de lectura es deliberadamente plana — `EntityControllerBase.cs` lo dice en
su propio comentario:

> "Cualquier rol autenticado puede leer; solo estos roles pueden mutar."

`GetAll()`/`GetById()` no son `virtual` y no consultan ningún rol: todo usuario autenticado ve
la tabla completa de cualquier entidad, y `CreateRoles`/`UpdateRoles`/`DeleteRoles` solo deciden
quién puede *escribir*. Restringir qué ve Delivery de `Pedido` es, por lo tanto, un caso nuevo
que la base no contempla — no alcanza con declarar un rol y dejarlo fuera de los `*Roles` de
siempre.

## 2. Decisión: `GetAll`/`GetById` de `Pedido` se filtran por rol; el resto de la app, no

Se hace `virtual` a `GetAll()`/`GetById()` en `EntityControllerBase<TEntity>` (cambio aditivo:
ningún controlador que no los sobrescriba cambia de comportamiento) y `PedidosController` los
sobrescribe para aplicar el filtro. **Ningún otro controlador se toca.**

Se decidió así y no, por ejemplo, agregando el filtro como una capa genérica en la base, porque
la restricción es puntual a una sola entidad y a un solo rol — generalizarla habría sido
resolver un problema que no existe todavía, y la propia base ya documenta que "cualquier rol
autenticado puede leer" es una decisión, no un descuido.

### 2.1 El filtro

```csharp
// Delivery es el único rol con una restricción de LECTURA (SCRUM-13): solo ve los pedidos
// listos para repartir. Es la ÚNICA restricción de este rol — no tiene CreateRoles,
// UpdateRoles ni DeleteRoles propios en ningún controlador, así que para todo lo demás
// (que hoy es "nada": no puede escribir en ninguna entidad) se comporta como cualquier
// usuario autenticado sin rol elevado.
//
// Se escribe como "el usuario no tiene ningún rol más amplio" y no como "el usuario ES
// Delivery", por el mismo motivo que ya separa RolesQueAmplian de una comparación directa:
// el corte es por capacidad, no por identidad (ver GetDelTurno_MeseroQueTambienEsAdmin_Pasa).
// Hoy Usuario.Rol es un único valor por usuario (JwtTokenGenerator emite un solo Claim de
// rol), así que en la práctica un usuario Delivery nunca tiene además Admin/Cajero/
// Cocinero/Mesero — pero el código no depende de esa invariante para ser correcto.
private static readonly string[] RolesSinRestriccionDePedidos =
    ["Admin", "Cajero", "Cocinero", "Mesero"];

private IEnumerable<Pedido> AplicarRestriccionDelivery(IEnumerable<Pedido> pedidos) =>
    HasAnyRole(RolesSinRestriccionDePedidos)
        ? pedidos
        : pedidos.Where(p => p.Estado == EstadoPedido.Servido && p.Tipo == TipoPedido.Delivery);
```

### 2.2 Dónde se aplica

**`GetAll`/`GetById` (el CRUD genérico, `GET api/pedidos` y `GET api/pedidos/{id}`)** —
defensa en profundidad: aunque la interfaz de Delivery nunca navega a `Pedidos/Edit.razor`
(§7), alguien que llame a la API directamente (Postman, curl) con un token de Delivery no
debería poder leer un pedido fuera de su vista.

```csharp
public override async Task<ActionResult<IEnumerable<Pedido>>> GetAll() =>
    Ok(AplicarRestriccionDelivery(await _service.GetAllAsync()));

public override async Task<ActionResult<Pedido>> GetById(long id)
{
    var entity = await _service.GetByIdAsync(id);
    if (entity is null)
        return NotFound();

    // 404 y no 403: para Delivery, un pedido que no cumple la restricción no existe en su
    // vista, igual que ArmarGrillaAsync ya lo excluye de la lista en vez de listarlo con un
    // candado. Un 403 confirmaría que el id corresponde a un pedido real; un 404 no revela
    // nada.
    return AplicarRestriccionDelivery([entity]).Any() ? Ok(entity) : NotFound();
}
```

**`ArmarGrillaAsync`** — el camino real que usa `Pedidos/Index.razor` vía
`TurnoApi.GetGrillaAbiertaAsync()`/`GetGrillaAsync()`:

```csharp
private async Task<GrillaPedidosDto> ArmarGrillaAsync(TurnoCaja turno)
{
    var pedidos = AplicarRestriccionDelivery(
        await _service.FindAsync(p => p.IdTurnoCaja == turno.Id)).ToList();
    // ... resto sin cambios (mesasPorPedido, TurnoDto, etc.)
}
```

`TotalPedidos` sigue saliendo de `pedidos.Count` (sin cambios en esa línea): para Delivery eso
ahora cuenta *sus* pedidos visibles, no el total del turno — es lo correcto, no un efecto
secundario a corregir. Mostrarle "14 pedidos" cuando ve 2 filas sería peor que mostrarle "2".

### 2.3 Qué NO se filtra, a propósito

`RolesQueAmplian = ["Admin", "Cajero", "Cocinero"]` (quién puede pedir un turno que no es el
abierto, `GetDelTurno`) **no cambia**: Delivery queda excluido, igual que Mesero ya lo estaba.
No hace falta agregar a Delivery a ninguna lista de exclusión nueva — alcanza con no agregarlo
a la de inclusión, que es como ya funciona para Mesero.

## 3. Dominio y base de datos

### 3.1 El enum

`Atipico.Domain/Enums/RolUsuario.cs`, mismo patrón que cualquier otro enum del proyecto
(PascalCase en C#, `UPPER_SNAKE_CASE` en la base):

```csharp
public enum RolUsuario
{
    Mesero,
    Cajero,
    Cocinero,
    Admin,
    Delivery
}
```

### 3.2 El `CHECK` — se edita en una migración nueva, no en `script_inicial.sql`

`ck_usuario_rol` (`sql/script_inicial.sql`) hoy es:

```sql
CONSTRAINT ck_usuario_rol CHECK (rol IN ('MESERO','CAJERO','COCINERO','ADMIN'))
```

`script_inicial.sql` no se edita — es el más viejo y "no se edita una vez aplicado" (mismo
criterio que ya documenta `Atipico.Infraestructure.Tests/ModeloEnumsCheckTest.cs` sobre la
precedencia de migraciones). El cambio va en:

```sql
-- sql/014_rol_delivery.sql
BEGIN;

-- SCRUM-13: agrega el rol Delivery. ck_usuario_rol espeja a mano RolUsuario (ver el
-- comentario de mantenimiento en script_inicial.sql) — hay que tocar los dos.
ALTER TABLE usuario DROP CONSTRAINT ck_usuario_rol;
ALTER TABLE usuario
    ADD CONSTRAINT ck_usuario_rol
    CHECK (rol IN ('MESERO','CAJERO','COCINERO','ADMIN','DELIVERY'));

COMMIT;
```

**Esto ya está cubierto por una prueba que existe hoy**, sin escribir ninguna nueva:
`Atipico.Infraestructure.Tests/ModeloEnumsCheckTest.cs` compara, para `(Usuario, Rol,
ck_usuario_rol)`, que el enum de C# y el `CHECK` tengan *exactamente* los mismos literales.
Apenas se agregue `Delivery` al enum, ese test **se pone en rojo** (el `CHECK` en la migración
más alta —todavía `012`/`013`, ninguna toca `usuario`— sigue sin `DELIVERY`) hasta que
`014_rol_delivery.sql` exista. Es la misma mecánica que ya usaron SCRUM-16/17/18 con sus
propios tests de migración: el archivo nuevo hace pasar un test que ya estaba escrito, no uno
que haya que inventar.

### 3.3 Sin cambios en `Atipico.Infraestructure`

`UsuarioConfiguration.cs` ya mapea `Rol` con `UpperSnakeCaseEnumConverter<RolUsuario>()`, que es
genérico sobre los valores del enum — no necesita saber que existe `Delivery`.

## 4. Interfaz de administración de usuarios

`Usuarios/Edit.razor` arma el `<select>` de rol con `Enum.GetValues<RolUsuario>()` — **sin
código que tocar**, `Delivery` aparece solo en cuanto existe en el enum. Se muestra como
`Delivery` a secas: no hay un `RolUsuarioExtensions.Etiqueta()` como el que sí tienen
`TipoPedido`/`EstadoPedido` (el rol nunca se mostró traducido en ningún lado), así que no hace
falta crear uno para mantener consistencia con lo que ya existe.

## 5. Navegación: Delivery necesita llegar a `/pedidos`

`Atipico.Web/Services/Navegacion.cs` es la única fuente del menú (sidebar + barra inferior
móvil). Hoy el link "Pedidos" es `Admin, Mesero`. Sin agregar `Delivery` ahí, el rol quedaría
sin ninguna forma de llegar a la pantalla que este mismo ticket le habilita en la API — un
rol que no rompe nada, pero que tampoco sirve para nada.

```diff
+ private const string Delivery = "Delivery";
  ...
  new("Pedidos",
  [
-     new("Pedidos", "pedidos", Admin, Mesero),
+     new("Pedidos", "pedidos", Admin, Mesero, Delivery),
      new("Pedido <-> Mesa", "pedido-mesas", Admin),
      new("Pedido <-> Plato", "pedido-platos", Admin),
  ]),
  ...
  public static readonly IReadOnlyList<EnlaceNav> Pestanas =
  [
-     new("Pedidos", "pedidos", Admin, Mesero),
+     new("Pedidos", "pedidos", Admin, Mesero, Delivery),
      new("Caja", "cuentas", Admin, Cajero),
  ];
```

`RolesCsv` de la sección "Pedidos" se deriva automáticamente (es la unión de los roles de sus
links, `SeccionNav.RolesCsv`) — no hay una segunda lista que sincronizar a mano.

## 6. `Pedidos/Index.razor`: Delivery se comporta como Mesero para el turno, no para las filas

La página en sí **no necesita saber que Delivery existe** para ver menos filas — eso ya lo
resuelve la API (§2.2): `_pedidos` llega pre-filtrado. Lo que sí hay que ajustar son los dos
lugares donde la página distingue "puede ampliar más allá del turno abierto" mirando
específicamente `"Mesero"`, dejando a Delivery en la rama equivocada (la que SÍ ofrece
historial, que la API de todos modos le va a rechazar con `RolesQueAmplian`):

```diff
  protected override async Task OnInitializedAsync()
  {
      ...
      if (AuthStateTask is not null)
      {
          var user = (await AuthStateTask).User;
-         if (!user.IsInRole("Mesero") || user.IsInRole("Admin"))
+         if ((!user.IsInRole("Mesero") && !user.IsInRole("Delivery")) || user.IsInRole("Admin"))
              await CargarTurnosAsync();
      }
```

```diff
- <AuthorizeView Roles="Mesero">
+ <AuthorizeView Roles="Mesero,Delivery">
      <NotAuthorized>
          <div class="col-12 col-md-3">
              <label class="form-label">Turno</label>
              ...
```

(y el segundo bloque equivalente, el de los filtros de Mesero/Creado, con el mismo cambio de
`Roles="Mesero"` a `Roles="Mesero,Delivery"`).

Sin este cambio la pantalla no se rompe —el backend igual rechaza el pedido de historial con
`Forbid()`— pero le ofrecería a Delivery un control que sabe de antemano que va a fallar, el
mismo error de UX que motivó separar "esto es maquetado, la API vuelve a validar" en el resto
del código.

### 6.1 Lo que se deja tal cual, a propósito

- Los chips de estado (`_estadosFiltrables`) y el filtro de Tipo: Delivery solo va a ver
  `Servido`/`Delivery` sin importar qué elija, así que estos controles quedan "sobrando" para
  ese rol, no rotos. Redecorar los filtros según el rol es más alcance del que pide el ticket.
- `EntityTable`'s `WriteRoles="@(["Admin", "Mesero"])"` de la propia grilla: sin cambios.
  Delivery no está en la lista, así que ya no ve "+ Nuevo" ni "Editar" — exactamente lo que
  corresponde a un rol de solo lectura, gratis, sin tocar nada.
- La columna "Mesa" (SCRUM-16): un pedido de delivery no tiene mesa asociada, así que Delivery
  va a ver "—" en esa columna siempre. No amerita ocultar la columna para este rol.

## 7. `Home.razor`: una sección más en la lista de roles autorizados

```diff
- <AuthorizeView Roles="Admin,Mesero">
+ <AuthorizeView Roles="Admin,Mesero,Delivery">
      <Authorized>
          <div class="d-flex align-items-baseline justify-content-between mb-2">
              <h2 class="panel-seccion">@(_soloMios ? "Tus pedidos activos" : "Pedidos activos")</h2>
              ...
```

Solo el bloque de "Pedidos activos" (el que lista `_activos`) — **no** el de "+ Nuevo pedido"
(sigue `Admin,Mesero`, Delivery no crea pedidos) ni el de "Atajos" (sigue `Admin,Cajero`, ningún
atajo de esa lista le sirve a Delivery).

`_pedidos` sale de `TurnoApi.GetGrillaAbiertaAsync()`, la misma llamada que ya filtra el
backend — Delivery ve ahí sus pedidos listos para repartir sin que `Home.razor` necesite saber
nada de la restricción. `_soloMios` se queda en `false` para Delivery (su condición sigue
siendo `Mesero && !Admin`): el título genérico "Pedidos activos" es correcto, no hay una noción
de "pedidos propios" para este rol.

## 8. Qué queda fuera, a sabiendas

**Otras entidades no se restringen.** `GET api/pedido-platos`, `GET api/detalle-cuentas`,
`GET api/cuentas`, etc. le siguen devolviendo la tabla completa a un usuario Delivery que las
llame directamente (no por la UI, que no lo navega ahí). El ticket pide una restricción sobre
Pedido, no un rediseño de autorización de lectura en toda la aplicación; ampliar el alcance a
"todo lo que se pueda inferir de un pedido oculto" sería resolver un problema que nadie planteó
todavía, siguiendo exactamente el mismo criterio que en `specs/tipo-pedido.md` §4 y
`specs/mesa-compartida-por-turno.md` §2 ya usaron para no agregar reglas no pedidas. Se deja
anotado por si en algún momento se decide que sí importa.

**El detalle del pedido (dirección de entrega) queda fuera.** Confirmado con el usuario durante
el análisis (§9, Bitácora): sin esto, Delivery ve *que* hay un pedido listo, pero no *a dónde*
llevarlo. Es un hueco operativo real y conocido, no un descuido — se prefirió no ampliar el
alcance de este ticket a costa de auditar cada botón de `Pedidos/Edit.razor` para ese rol.

## 9. Plan de implementación

| # | Capa | Archivo | Cambio |
|---|---|---|---|
| 1 | Dominio | `Atipico.Domain/Enums/RolUsuario.cs` | agregar `Delivery` (§3.1) |
| 2 | SQL | `sql/014_rol_delivery.sql` | `DROP`/`ADD CONSTRAINT ck_usuario_rol` (§3.2) |
| 3 | Api | `Atipico.Api/Controllers/EntityControllerBase.cs` | `GetAll`/`GetById` pasan a `virtual` (§2) |
| 4 | Api | `Atipico.Api/Controllers/PedidosController.cs` | `AplicarRestriccionDelivery`, overrides de `GetAll`/`GetById`, filtro en `ArmarGrillaAsync` (§2.1–2.2) |
| 5 | Web | `Atipico.Web/Services/Navegacion.cs` | agregar `Delivery` al link "Pedidos" y a `Pestanas` (§5) |
| 6 | Web | `Components/Pages/Pedidos/Index.razor` | los dos ajustes de §6 |
| 7 | Web | `Components/Pages/Home.razor` | agregar `Delivery` al `AuthorizeView` de "Pedidos activos" (§7) |

No hay cambios en `Atipico.Infraestructure` (§3.3) ni en `Usuarios/Edit.razor` (§4).

## 10. Plan de pruebas

**Ya cubierto, sin escribir nada nuevo:** `ModeloEnumsCheckTest.ElEnumYElCheckTienenExactamenteLosMismosLiterales`
y `CadaLiteralDelCheckVuelveASuValorDelEnum`, para `(Usuario, Rol, ck_usuario_rol)` — van a
fallar apenas se agregue `Delivery` al enum, y volver a verde apenas exista
`sql/014_rol_delivery.sql` (§3.2).

**Nuevo, en `Atipico.Api.Tests`** (`PedidosControllerTurnoTests.cs` u otro archivo, a decisión
de quien escriba los tests — el patrón de mocks/`ConRol` ya existe ahí):

1. `GetAll` con rol `Delivery` (y ningún otro) devuelve solo los pedidos con
   `Estado = Servido` y `Tipo = Delivery`.
2. `GetAll` con `Admin`/`Cajero`/`Cocinero`/`Mesero` sigue devolviendo todo sin filtrar —
   regresión explícita de que el filtro no se activa de más.
3. `GetById` con rol `Delivery` sobre un pedido que **no** cumple la restricción devuelve
   `NotFound` (no `Forbid`).
4. `GetById` con rol `Delivery` sobre un pedido que sí cumple devuelve `Ok` con la entidad.
5. `GetDelTurnoAbierto` con rol `Delivery`: la `GrillaPedidosDto` resultante solo trae los
   pedidos filtrados, y `Turno.TotalPedidos` cuenta esos, no el total del turno (§2.2).

**Nuevo, en `Atipico.Web.Tests`** (mismo patrón bUnit que `PedidosIndexMesaTests.cs` /
`PedidosIndexOrdenNumeroTests.cs`):

6. Con rol `Delivery`, la grilla de `/pedidos` no muestra el selector de turno histórico ni los
   filtros de Mesero/Creado (§6) — mismo caso que ya valdría la pena tener para `Mesero` si no
   existe (verificar antes de asumir que hace falta escribirlo desde cero).

**No se escribe** ningún test de `Navegacion.cs` ni de `Home.razor`: no hay un precedente de
test para esos archivos en el repo (`Atipico.Web.Tests` no los cubre hoy), y agregar el primero
para este cambio puntual sería más alcance del que amerita una lista de constantes y un
`AuthorizeView` de más.

## 11. Puesta en producción

1. Correr `sql/014_rol_delivery.sql` contra la base de producción.
2. Crear al menos un `Usuario` con `Rol = Delivery` para poder probar el flujo end-to-end
   (`Usuarios/Edit.razor`, sin cambios, ya lo permite). Si el entorno de desarrollo usa
   `sql/dev_datos_iniciales.sql` para sembrar usuarios de prueba, agregar uno ahí es opcional y
   no bloquea esta funcionalidad.
3. No hay variables de entorno ni configuración nueva.

## 12. Bitácora

- **2026-09-05.** Análisis inicial a partir de la descripción completa de SCRUM-13 (una sola
  oración). Se identificó que la restricción pedida ("solo ve pedidos ya preparados") no tiene
  precedente en el proyecto: toda lectura hoy es `[Authorize]`-only, sin filtrar filas por rol
  (`EntityControllerBase.GetAll`/`GetById` no son `virtual`). Antes de diseñar la solución se
  consultaron dos puntos que el ticket no especifica y que cambian la implementación:
  - **Qué cuenta como "ya preparado":** se eligió `Estado = Servido` (ventana operativa real
    de "listo para repartir, todavía no cerrado"), no `Servido` + `Cerrado`.
  - **Si el filtro también corta por `Tipo`:** se eligió sí, solo `Tipo = Delivery` — un
    repartidor no necesita ver pedidos en salón o para llevar.
  - **Si Delivery puede abrir el detalle del pedido** (`Pedidos/Edit.razor`) para ver la
    dirección de entrega: se eligió que no, por ahora — queda fuera de este ticket (§8), a
    pesar de ser una limitación operativa real, para no ampliar el alcance a auditar cada
    botón de esa pantalla por rol.
  Pendiente de aprobación antes de tocar código.
