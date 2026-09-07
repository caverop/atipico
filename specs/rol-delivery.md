# Rol Delivery: solo ve pedidos ya preparados

Especificación funcional y técnica para SCRUM-13 (Jira, proyecto `atipico`): "Crear delivery
como un rol y configurar sus permisos". Documento de referencia previo a la implementación:
recoge las decisiones tomadas y su porqué, para no volver a discutirlas al escribir el código.

- **Estado:** **en producción**, completo. `sql/014_rol_delivery.sql` corrido; §1–§7 (grilla
  filtrada) y §8 (ficha de solo lectura) verificados en pantalla por el usuario; 199 pruebas en
  verde.
- **Origen:** [SCRUM-13](https://caverop.atlassian.net/browse/SCRUM-13), tipo Task. Descripción
  completa del ticket: *"Se necesita agregar un nuevo rol, con solamente una restriccion, este
  solo puede ver pedido ya preparados."*
- **Alcance:** un rol `Delivery` que solo ve los pedidos con `Estado = Servido` y
  `Tipo = Delivery` —los que están listos para repartir—, tanto en la grilla como al abrir la
  ficha de uno. Sin permisos de escritura en ningún lado (no crea, no edita, no anula nada).
- **Fuera de alcance:**
  - No se restringe el acceso a ninguna otra entidad (`PedidoPlato`, `DetalleCuenta`, `Cuenta`,
    `Mesa`, `Plato`, `Usuario`, ...): la única restricción que pide el ticket es sobre Pedido,
    y no se inventan restricciones adicionales que nadie pidió (§8.3 detalla el hueco que esto
    deja, a sabiendas).
  - No se agrega una acción para que Delivery marque un pedido como entregado/cerrado. Entra a
    **ver**, no a operar.

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
- `WriteRoles="@(["Admin", "Mesero"])"` sigue **sin** Delivery: no crea ni borra nada. Lo que
  sí cambia con la ampliación de §8 es poder **abrir** una fila, que hasta ahora `EntityTable`
  trataba como lo mismo que escribir (§8.2).
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

**Las filas del panel vuelven a ser links para todos los roles que ven la sección.** Entre la
primera implementación y esta ampliación hubo un arreglo intermedio que las dejaba planas (sin
`href`) para Delivery, porque el link llevaba a una pantalla que su rol no podía abrir y
`AuthorizeRouteView` lo mandaba a `RedirectToLogin` — logueado, pero rebotado al login. Con §8
esa pantalla pasa a estar permitida, así que el arreglo deja de tener sentido y se revierte
(junto con la regla `.panel-fila-plana:hover` que se había agregado a `app.css` para él). Ver
§12, Bitácora.

## 8. El detalle del pedido: Delivery entra a ver, no a editar

**Ampliación de alcance.** La versión original de este documento dejaba `Pedidos/Edit.razor`
fuera a propósito, para no tener que auditar cada botón de esa pantalla por rol. Al probarlo
apareció lo obvio: sin la ficha, Delivery ve *que* hay un pedido listo pero no *a dónde*
llevarlo — la dirección, el mapa y el enlace "Abrir para navegar" viven todos ahí. Se amplía.

Y la auditoría resultó mucho más chica de lo temido, por una razón que no era evidente cuando
se decidió postergarla: **Delivery solo puede abrir pedidos en `Servido`** (§2.2 lo garantiza:
`GetById` devuelve 404 para cualquier otro), y casi todo el formulario ya está apagado por
estado para un pedido que no está `Abierto`.

### 8.1 Qué queda vivo, y por lo tanto hay que apagar

Recorriendo la pantalla con un pedido `Servido` en la mano:

| Control | Gate actual | Con `Servido` |
|---|---|---|
| "Quitar" plato · "Agregar plato" · zona de comprobantes QR | `_pedidoActivo` (`Estado == Abierto`) | ya oculto ✅ |
| `<select>` Tipo de pedido | `disabled` si `Id is not null && !_pedidoActivo` | ya deshabilitado ✅ |
| Campos de Entrega (referencia, ubicación, coordenadas) | `_entregaBloqueada` | ya deshabilitados ✅ |
| Mapa + "Abrir para navegar" | sin gate (es lectura) | visible ✅ **y es lo que Delivery viene a buscar** |
| Lista de platos + Subtotal | sin gate (es lectura) | visible ✅ **"verificar el pedido"** |
| Método de pago + "En Preparación" | `BarraConMetodo` = `_pedidoActivo` | ya oculto ✅ |
| "Servido" | `_puedeMarcarServido` (`Estado == EnPreparacion`) | ya oculto ✅ |
| **"Anular"** | `_puedeAnular` (`Estado != Cerrado/Anulado`) | **VIVO** ⚠️ |
| **Sección Mesas ("Agregar"/"Quitar")** | `_puedeAsociarMesa` (idem) | **VIVO** ⚠️ (solo si el pedido tiene mesas: `_mostrarMesas`) |
| **Campo Comensal** | ninguno — siempre editable | **VIVO** ⚠️ |
| **`ComprobantesPanel`** ("Reemplazar" + subir) | `PuedeEditar = cuenta.Estado != Anulada` | **VIVO** ⚠️ |

Son **cuatro** puntos, no la pantalla entera. Se apagan con una sola bandera derivada del rol,
calculada en `OnInitializedAsync` a partir del `AuthenticationState` que la página ya recibe:

```csharp
// Delivery entra a ver, no a editar (§8). El corte es por capacidad y no por identidad, igual
// que en PedidosController: se pregunta por los roles que SI editan, no por "es Delivery".
private bool _puedeEditar = true;
...
_puedeEditar = user.IsInRole("Admin") || user.IsInRole("Mesero");
```

y se conecta en cuatro lugares:

```diff
- private bool _puedeAnular =>
-     Id is not null && _entity.Estado != EstadoPedido.Cerrado && _entity.Estado != EstadoPedido.Anulado;
+ private bool _puedeAnular =>
+     _puedeEditar && Id is not null && _entity.Estado != EstadoPedido.Cerrado && _entity.Estado != EstadoPedido.Anulado;

- private bool _puedeAsociarMesa =>
-     Id is not null && _entity.Estado != EstadoPedido.Cerrado && _entity.Estado != EstadoPedido.Anulado;
+ private bool _puedeAsociarMesa =>
+     _puedeEditar && Id is not null && _entity.Estado != EstadoPedido.Cerrado && _entity.Estado != EstadoPedido.Anulado;

- <InputText class="form-control" @bind-Value="_entity.Comensal" />
+ <InputText class="form-control" @bind-Value="_entity.Comensal" disabled="@(!_puedeEditar)" />

- PuedeEditar="@(cuenta.Estado != EstadoCuenta.Anulada)"
+ PuedeEditar="@(_puedeEditar && cuenta.Estado != EstadoCuenta.Anulada)"
```

`HayAcciones` y `BarraConMetodo` se derivan de esos flags, así que la barra de acciones entera
—y su espaciador— desaparecen solos para Delivery, sin tocarlos.

**Un guard extra en `HandleSave`**, que no tiene control visible que ocultar: el `EditForm` no
dibuja botón de submit cuando el pedido ya existe, pero **Enter en un campo de texto igual
envía el formulario** (está documentado en el propio comentario del componente). Con el campo
Comensal deshabilitado ya no hay dónde tipear, pero el guard cierra el caso por si mañana se
agrega otro campo:

```csharp
private async Task HandleSave()
{
    if (!_puedeEditar) return;
    ...
```

**Un mensaje que hay que apagar junto con el botón.** La sección de mesas termina con un `else`
del `_puedeAsociarMesa` que explica *por qué* no hay botón: *"El pedido ya está cerrado o
anulado; no se puede cambiar la mesa."* Ese texto explica un **estado**, y en cuanto
`_puedeAsociarMesa` también dependa del rol pasa a mentirle a Delivery, que está mirando un
pedido `Servido`. Se gatea para que solo lo vea quien podría editar si el estado se lo
permitiera:

```diff
- else
- {
-     <p class="text-muted"><em>El pedido ya está cerrado o anulado; no se puede cambiar la mesa.</em></p>
- }
+ else if (_puedeEditar)
+ {
+     <p class="text-muted"><em>El pedido ya está cerrado o anulado; no se puede cambiar la mesa.</em></p>
+ }
```

Para el que solo mira, la lista de mesas es informativa y no necesita explicar la ausencia de
un botón: en su pantalla no hay ningún botón, y justificar cada uno que falta sería ruido.

Ojo con el mensaje hermano de la sección de platos (*"El pedido ya no está abierto; no se
pueden agregar más platos"*): ese cuelga de `_pedidoActivo`, que **no** se gatea por rol (ver
abajo), así que para Delivery sigue siendo cierto —el pedido efectivamente no está `Abierto`—
y se deja tal cual. La asimetría no es un descuido: es la consecuencia de que uno de los dos
flags mezcle permiso y el otro no.

**Por qué no se gatea también `_pedidoActivo`:** es un predicado de *estado* ("el pedido está
Abierto"), no de permiso, y su nombre lo dice. Mezclarle el rol lo volvería mentiroso y no
cambiaría nada observable: la API garantiza que Delivery nunca carga un pedido que no esté
`Servido`, así que todo lo que cuelga de `_pedidoActivo` ya está apagado. La defensa real de
esos caminos no es la UI sino el servidor: `UpdateRoles`/`CreateRoles` de
`PedidosController`, `PedidoPlatosController` y `PedidoMesasController` no incluyen a Delivery,
así que cualquier escritura suya termina en 403 aunque alguien fabrique el request a mano.

### 8.2 Cómo llega Delivery al detalle desde la grilla

Hoy `EntityTable` trata **"poder abrir una fila" y "poder escribir" como lo mismo**: `WriteRoles`
decide el botón "+ Nuevo", el botón de borrar, el link de la tarjeta en móvil y el botón
"Editar" de la columna de acciones en escritorio. En las nueve grillas eso siempre coincidió.
Delivery rompe la coincidencia: entra a ver, pero no escribe.

Se separan con **dos parámetros opcionales nuevos**, que por defecto reproducen exactamente lo
de hoy (`DetalleRoles ?? WriteRoles`), así que las otras ocho pantallas no cambian ni una línea:

```csharp
/// <summary>
/// Roles que pueden ABRIR la ficha de una fila. Por defecto, los mismos que escriben: hasta
/// SCRUM-13 "entrar" y "editar" eran lo mismo en todas las grillas. Delivery es el primer rol
/// que entra sin escribir, y meterlo en WriteRoles habría encendido tambien "+ Nuevo" y el
/// boton de borrar.
/// </summary>
[Parameter] public string[]? DetalleRoles { get; set; }

/// <summary>Texto del boton que abre la ficha. La pagina lo cambia a "Ver" para quien no edita.</summary>
[Parameter] public string DetalleLabel { get; set; } = "Editar";

private string DetalleRolesCsv => string.Join(",", DetalleRoles ?? WriteRoles);
```

Dentro de `EntityTable`, `DetalleRolesCsv` reemplaza a `RolesCsv` en **tres** lugares —el link
de la tarjeta móvil, el `<td>` de acciones y el `<th>` vacío de su encabezado— y `RolesCsv`
se queda gobernando "+ Nuevo" y el botón de borrar, que pasan a llevar su propio
`AuthorizeView` anidado.

> ⚠️ El `<th>` y el `<td>` **tienen que usar el mismo rol**. Si uno se cambia y el otro no, la
> tabla queda con una columna de diferencia entre encabezado y cuerpo para el rol afectado. Es
> el único error de esta sección que no se ve en un test de lógica y sí en pantalla.

Y `Pedidos/Index.razor` los pasa:

```razor
DetalleRoles="@(["Admin", "Mesero", "Delivery"])"
DetalleLabel="@(_puedeEditarPedidos ? "Editar" : "Ver")"
```

con `_puedeEditarPedidos` calculado igual que `_puedeEditar` de §8.1, en el mismo
`OnInitializedAsync` donde la página ya consulta el `AuthenticationState`. La etiqueta importa:
un botón que dice "Editar" y abre una ficha de solo lectura es la clase de mentira de interfaz
que el resto del código evita a propósito.

### 8.3 Lo que sigue quedando fuera

**Otras entidades no se restringen.** `GET api/pedido-platos`, `GET api/detalle-cuentas`,
`GET api/cuentas`, etc. le siguen devolviendo la tabla completa a un usuario Delivery que las
llame directamente (no por la UI, que no lo navega ahí). Nota: con §8, `Pedidos/Edit.razor`
**usa** esos endpoints para armar la ficha (platos, cuentas QR, mesas), así que esta holgura
deja de ser teórica y pasa a ser algo de lo que la pantalla depende. Sigue siendo lo pedido —el
ticket restringe Pedido, no rediseña la autorización de lectura de toda la aplicación—, pero
conviene tenerlo escrito: el día que se quiera cerrar ese hueco, hay que hacerlo sin romper
esta ficha.

**Marcar el pedido como entregado.** Delivery no puede cambiar el estado de nada. Si más
adelante hace falta un "Entregado", es una transición de estado nueva (con su columna, su
`CHECK` y su regla de negocio), no un botón más en esta pantalla.

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

**Ampliación §8** (pendiente):

| # | Capa | Archivo | Cambio |
|---|---|---|---|
| 8 | Web | `Components/Pages/Pedidos/Edit.razor` | `[Authorize]` suma `Delivery`; bandera `_puedeEditar` y sus cuatro conexiones + guard en `HandleSave` (§8.1) |
| 9 | Web | `Components/Shared/EntityTable.razor` | parámetros `DetalleRoles`/`DetalleLabel`; separar "abrir" de "escribir" en tarjeta móvil, `<td>` y `<th>` (§8.2) |
| 10 | Web | `Components/Pages/Pedidos/Index.razor` | pasar `DetalleRoles`/`DetalleLabel`, con `_puedeEditarPedidos` (§8.2) |
| 11 | Web | `Components/Pages/Home.razor` + `wwwroot/app.css` | revertir la fila plana y `.panel-fila-plana:hover` (§7) |

No hay cambios en `Atipico.Infraestructure` (§3.3), en `Usuarios/Edit.razor` (§4) ni en la API
para la ampliación: `GetById` ya devuelve 404 para un pedido que Delivery no puede ver (§2.2),
y `EntityApiClient.GetByIdAsync` traduce ese 404 a `null`, que `Edit.razor` ya resuelve
redirigiendo a `/pedidos`. Ese camino no hay que escribirlo, pero sí conviene probarlo (§10).

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

### 10.1 Ampliación §8

Todo en `Atipico.Web.Tests`, mismo patrón bUnit que los archivos ya existentes. La ficha
(`Pedidos/Edit.razor`) inyecta diez servicios; `PedidosEditMesaCompartidaTests.cs` (SCRUM-17) ya
tiene armado ese `PrepararDependencias` — conviene partir de ahí en vez de rehacerlo.

7. **Con rol `Delivery`, sobre un pedido `Servido`+`Delivery`, la ficha se abre** (no
   redirige) y muestra lo que el repartidor viene a buscar: la lista de platos y el bloque de
   Entrega con el enlace "Abrir para navegar".
8. **Y no ofrece ninguna acción de escritura:** no aparece el botón "Anular", ni el de
   "Agregar mesa", ni la barra de acciones; el campo Comensal está `disabled` (§8.1). Es el
   test que hay que escribir con más cuidado: son cuatro aserciones de ausencia, y una
   aserción de ausencia pasa también cuando la pantalla no se renderizó por otro motivo — hay
   que anclarla con al menos una aserción positiva (que la ficha efectivamente cargó, del caso
   7) para que no dé un falso verde.
9. **Con rol `Admin` (o `Mesero`) la misma ficha sigue mostrando esas acciones** — regresión
   explícita de que `_puedeEditar` no apagó de más. Sin este caso, el test 8 pasaría igual si
   alguien rompiera la pantalla para todos.
10. **`EntityTable` con `DetalleRoles` sin declarar se comporta igual que hoy** (cae en
    `WriteRoles`): es la garantía de que las otras ocho grillas no cambiaron. Puede escribirse
    contra `EntityTable` directamente con una entidad de prueba, o apoyarse en los tests que ya
    existen de otras grillas si los hay.
11. **En `/pedidos` con rol `Delivery`, la fila lleva a la ficha y el botón dice "Ver"**; con
    `Admin`, dice "Editar" (§8.2).

**No se cubre con test** el caso "Delivery abre por URL un pedido que no le corresponde": el
404 lo produce la API (ya cubierto por el caso 3 en `Atipico.Api.Tests`) y la redirección a
`/pedidos` es código preexistente de `Edit.razor` que este ticket no toca. Vale probarlo a mano
al verificar en pantalla (§11).

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

- **2026-09-05, implementación de §1–§7.** QA escribió los tests en rojo y dev implementó los
  siete archivos del plan; 191 pruebas en verde. Al probar en pantalla aparecieron dos cosas:

  1. **Un bug preexistente, ajeno a este ticket**, que se llevó su propio arreglo directo: el
     `returnUrl` que arma `RedirectToLogin.razor` sale de `Nav.ToBaseRelativePath(...)`, que
     devuelve la ruta **sin barra inicial**, y `Results.LocalRedirect` (en el POST
     `/auth/login`) rechaza cualquier cosa que no empiece con `/` — así que volver a loguearse
     después de un redirect a `/login` reventaba con *"The supplied URL is not local"*. Se
     arregló en el origen (agregar la barra) y además se endureció el destino: el chequeo
     `Uri.IsWellFormedUriString(_, UriKind.Relative)` acepta `"pedidos/17"` y `"//host/..."`
     como relativos válidos, y se reemplazó por una validación que replica lo que
     `LocalRedirect` exige, cayendo a `"/"` en vez de tirar una excepción no capturada.
  2. **Una consecuencia directa del alcance elegido:** en el panel, cada pedido es un link a
     `Pedidos/Edit.razor`. Delivery veía la lista (§7) pero al hacer clic
     `AuthorizeRouteView` lo rechazaba por rol y `Routes.razor` lo mandaba a
     `RedirectToLogin` — es decir, al login, estando logueado. Se arregló dejando la fila plana
     (sin `href`) para los roles sin acceso a la ficha.

- **2026-09-05, ampliación de alcance (§8).** El usuario pidió habilitar a Delivery para entrar
  al pedido: sin la ficha no puede *verificar* qué lleva ni a dónde. Se revierte la decisión de
  postergarlo, y con ella el arreglo 2 de la entrada anterior (la fila vuelve a ser link, junto
  con su regla CSS). Al medir el trabajo real apareció el dato que faltaba cuando se decidió
  postergar: como Delivery **solo puede abrir pedidos `Servido`**, casi toda la pantalla ya
  está apagada por estado, y la superficie de escritura viva se reduce a cuatro puntos (§8.1)
  más la separación entre "abrir" y "escribir" en `EntityTable` (§8.2). Es bastante menos de lo
  que se temía al escribir la versión original de este documento — y vale registrarlo porque el
  motivo para postergar era justamente esa estimación, que resultó equivocada.

- **2026-09-05, implementación de §8.** QA escribió los cinco casos de §10.1 y encontró, antes
  de escribirlos, un falso verde que habría pasado desapercibido: **bUnit no evalúa el atributo
  `[Authorize]`** de un componente renderizado directo — ese chequeo lo hace `AuthorizeRouteView`
  (en `Routes.razor`), no el render. Un test que renderizara `Pedidos/Edit.razor` a secas con rol
  Delivery habría pasado **con el `[Authorize(Roles = "Admin,Mesero")]` intacto**, justo en el
  caso que §10.1 marcaba como el más delicado. El harness monta `AuthorizeRouteView` con el
  `RouteData` real y un marcador en su `NotAuthorized`. Queda anotado para el próximo test de
  página con roles.

  Dos trampas de Razor encontradas al implementar, ninguna detectada por el compilador:

  1. Un **comentario Razor entre los atributos de un componente** no es un comentario: se manda
     como nombre de parámetro. `dotnet build` da 0 errores y 0 warnings, y revienta en runtime
     con *"does not have a property matching the name '@\* ... \*@'"*. En bUnit se manifiesta
     como el fallo de **todos** los tests que rendericen esa página, lo que despista: parece que
     se rompió la pantalla entera y en realidad sobra un comentario. Va arriba de la etiqueta.
  2. Escribir la **secuencia de cierre de comentario dentro del propio comentario** (al intentar
     documentar la trampa 1) lo corta ahí mismo y el resto se compila como código, con un
     `CS1525` apuntando a líneas sin relación. `@@` no escapa.

  Verificado en pantalla por el usuario: la ficha abre para Delivery con platos y dirección, sin
  ninguna acción de escritura.
