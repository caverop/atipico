---
estado: propuesto
ticket: SCRUM-21
actualizado: 2026-09-16
afecta: [sql, Atipico.Domain, Atipico.Infraestructure, Atipico.Api, Atipico.Web]
---

# Reservas de mesa por el comensal

Especificación para SCRUM-21 ("Crear formulario para reservas").

- **Estado:** **propuesto**, pendiente de aprobación.
- **Alcance:** un formulario público (sin login) para que el comensal reserve una mesa —
  opcionalmente adjuntando un comprobante de pago/seña —, y una pantalla interna para que
  el personal confirme, cancele o cierre esas reservas. Confirmar una reserva genera
  automáticamente el `Pedido` correspondiente (§3.1, agregado 2026-09-16 al reconciliar
  con el flujo real pedido por el usuario en SCRUM-21).
- **Fuera de alcance:** notificaciones al comensal (email/SMS) cuando se confirma o
  cancela; que el comensal pueda ver, cambiar o cancelar su propia reserva después de
  crearla; detección de solapamiento por duración (la reserva no tiene un "hasta", solo un
  instante); un plano de mesas visual; corregir o reemplazar un comprobante de reserva ya
  subido (§3.1 explica por qué). Todo esto queda anotado en §7 para un ciclo próximo, no se
  diseña acá.

---

## 1. Problema

No existe ningún concepto de reserva en el sistema. Lo único que lo anticipa es
`mesa.estado`, que ya incluye `RESERVADA` en su `CHECK` (`ck_mesa_estado`,
`sql/script_inicial.sql:101`) y en el enum `EstadoMesa` (`Reservada`) — pero nunca se usó:
nada en la API ni en la Web pone una mesa en ese estado. Es una pieza que quedó a medio
diseñar desde el principio.

Decisiones tomadas con el usuario antes de diseñar el resto:

| Pregunta | Decisión |
|---|---|
| ¿Quién crea la reserva? | El comensal, público, sin login |
| ¿Aparta una mesa específica o solo un cupo? | Mesa específica |
| ¿Qué estados tiene? | `Pendiente` / `Confirmada` / `Cancelada` / `Cumplida` |

Todo lo que sigue (cómo se elige la mesa, quién gestiona los estados, qué endpoints son
anónimos) es diseño derivado de esas tres decisiones, no vuelto a preguntar.

### 1.1 Decisiones ampliadas (2026-09-16)

SCRUM-21 pedía además que "el mesero o cajero convierta la reserva en pedido" y que el
comensal pueda "adjuntar comprobante" — frase ambigua que se resolvió con el usuario antes
de tocar este documento (ver bitácora):

| Pregunta | Decisión |
|---|---|
| ¿"comensal realiza un pedido" es un lapsus? | Sí — sigue siendo `Reserva`, no `Pedido`. Nada cambia en quién crea qué. |
| ¿Qué es el comprobante? | Evidencia de pago/seña, mismo espíritu que [comprobantes-qr.md](comprobantes-qr.md), pero **no** puede ser una fila de `comprobante_pago` (exige `Cuenta`, que no existe en una reserva) — necesita su propio mecanismo, §3.1 |
| ¿Es obligatorio? | No se preguntó explícitamente. **Decisión derivada**: opcional — una reserva sin seña sigue siendo una reserva válida, el comprobante es evidencia adicional para que el personal decida si confirma |
| ¿Cuándo dispara la conversión a `Pedido`? | Al confirmar (`Pendiente` → `Confirmada`), no al cumplir |
| ¿Qué pasa con la mesa en ese momento? | Se queda `RESERVADA` — no pasa a `OCUPADA` hasta `Cumplida` (§3.1) |

Igual que las tres decisiones originales, todo lo que sigue en §3.1, §4, §6 y §7 es diseño
derivado de esta tabla, no vuelto a preguntar.

## 2. Por qué el comensal no elige la mesa a mano

"Mesa específica" significa que la fila de `reserva` tiene un `id_mesa` concreto desde que
se crea — no que el comensal navegue una lista de números de mesa. Un cliente público no
conoce el plano del local ni le importa qué mesa es la 7. Por eso: el comensal solo indica
**cuántas personas** y **cuándo**; el sistema **asigna automáticamente** la mesa más chica
que le entra (`capacidad >= numero_personas`, `estado <> 'INACTIVA'`), y responde con el
número de mesa asignado como confirmación de la solicitud.

Esto también evita tener que exponer un segundo endpoint anónimo para **listar mesas** —
motivo distinto del que sí justificó agregar un segundo endpoint público para el
comprobante (§3.1). Si más adelante se quiere que el comensal vea el plano y elija, es un
cambio de UI sobre el mismo endpoint (recibir `id_mesa` opcional en vez de calcularlo), no
un cambio de esquema.

## 3. Por qué "Pendiente" no bloquea la mesa

Si crear la reserva (público, sin revisión) ya pusiera la mesa en `RESERVADA`, cualquiera
podría bloquear mesas del local sin que el personal se entere. Por eso:

- **Al crear** (siempre `Pendiente`): se **valida** que exista una mesa con capacidad
  suficiente y no `INACTIVA`, pero **no se cambia su estado**. Pueden existir varias
  reservas `Pendiente` para la misma mesa/horario a la vez — son solicitudes, no promesas.
- **Al confirmar** (`Pendiente` → `Confirmada`, hace el personal): la mesa pasa a
  `RESERVADA`. Se rechaza si la mesa ya está `RESERVADA` u `OCUPADA` en ese momento — el
  personal ve el conflicto y decide (confirma otra mesa a mano, o cancela alguna de las
  solicitudes en conflicto). **Desde el 2026-09-16 esto también crea el `Pedido`
  correspondiente — ver §3.1, que reemplaza lo que decía antes este párrafo.**
- **Al cancelar** (solo alcanzable desde `Pendiente` — ver §3.1 sobre por qué `Confirmada`
  ya no puede cancelarse): si la mesa estaba `RESERVADA` por esta reserva, vuelve a `LIBRE`.
- **Al cumplir** (`Confirmada` → `Cumplida`, el cliente llegó): la mesa pasa de `RESERVADA`
  a `OCUPADA` — ya hay un `Pedido` abierto en curso, así que corresponde marcarla ocupada
  recién cuando el cliente está físicamente ahí, no antes. **Ver §3.1: esto reemplaza la
  versión anterior de este spec, que decía que `Cumplida` no tocaba la mesa.**

Es una simplificación consciente: no hay ventana horaria ("de 20:00 a 22:00"), solo un
instante. Comparar "mesa ya reservada" es mirar su `estado` actual, no calcular
solapamientos — ver §7.

### 3.1 Conversión automática a Pedido, y comprobante de pago (agregado 2026-09-16)

> SCRUM-21 pedía, en palabras del usuario: *"el comensal realiza un pedido y puede
> adjuntar comprobante, pero es el mesero o cajero quien convierte esa reserva en
> pedido"*. La primera versión de este spec (2026-09-08/09) no conectaba `Reserva` con
> `Pedido` en absoluto — este §3 decía explícitamente que `Cumplida` "no la pasa a
> `OCUPADA`, eso lo maneja el flujo de `Pedido` que ya existe". Esa frase ya no es cierta
> y queda reemplazada por lo que sigue.

**Qué se agrega:**

1. **Al confirmar** (`Pendiente` → `Confirmada`), además de lo que ya hacía (arriba), se
   crea automáticamente una fila `Pedido`: `Estado = Abierto`, `IdMesa = reserva.IdMesa`
   (la ya asignada al crear), `IdMesero = ` el usuario que confirma (`UsuarioActualId`,
   sea `Admin` o `Cajero` — el dominio no exige que `IdMesero` tenga rol `Mesero`, es solo
   el responsable registrado; se puede reasignar después desde `Pedidos/Edit.razor` como
   cualquier otro pedido), `Comensal = reserva.NombreCliente`, `TipoPedido = EnSalon`
   (siempre — una reserva es, por diseño, una mesa en el local). `reserva.IdPedido` se
   estampa con el id generado.
2. **Al cumplir** (`Confirmada` → `Cumplida`), la mesa pasa de `RESERVADA` a `OCUPADA` —
   ya no vuelve a `LIBRE` como antes, porque ahora siempre hay un `Pedido` en curso
   esperando al cliente.
3. **El comensal puede adjuntar un comprobante de pago/seña al crear la reserva**, mismo
   espíritu que [comprobantes-qr.md](comprobantes-qr.md) (evidencia de un pago, no un
   monto tecleado), pero con un mecanismo propio — ver por qué en el punto siguiente.

**Por qué el comprobante de reserva no reusa `comprobante_pago`.** Esa tabla tiene
`id_cuenta bigint NOT NULL REFERENCES cuenta`, y su trigger `fn_comprobante_inmutable`
exige que esa cuenta tenga `metodo_pago = 'QR'`. Una `Reserva` no tiene `Cuenta` — no hay
pedido, ni platos, ni cobro todavía. Forzar una `Cuenta` solo para colgar un comprobante
sería inventar un pago que no ocurrió. Por eso `reserva_comprobante` es una tabla nueva,
más simple, colgada de `reserva` en vez de `cuenta` (esquema en §4).

**Por qué es más simple que `comprobante_pago`, y no un calco.** `comprobante_pago`
resuelve tres problemas que acá no existen: (a) **varios comprobantes que suman** — una
reserva pide un aporte informal, no una conciliación contable, así que un comprobante
alcanza; (b) **reemplazo (`id_reemplaza`)** por si la foto sale mal — se deja **fuera de
alcance** (ver cabecera): si el comensal se equivoca, no hay forma de corregirlo desde acá,
llama al local o se resuelve en persona, porque el costo de construir esa cadena de
reemplazo para evidencia informal no está justificado; (c) **`id_subido_por`** — no
aplica, quien sube el archivo es el comensal anónimo, no un usuario logueado.

**Por qué no hace falta un trigger de inmutabilidad propio.** `fn_comprobante_inmutable`
existe para hacer cumplir una regla cruzada (la cuenta tiene que ser QR) y para bloquear
`UPDATE`/`DELETE`. `reserva_comprobante` no tiene una regla cruzada que cumplir, no tiene
ningún endpoint de `UPDATE`, y `app_restaurante` ya no tiene `GRANT DELETE` en ningún lado
del esquema — la tabla es de solo inserción por construcción, sin necesidad de un trigger
que lo repita.

**Por qué el upload es un segundo endpoint anónimo, y no va en el mismo POST.**
`ReservasController.Create` sobrescribe el `Create` heredado de
`EntityControllerBase<Reserva>` con la firma `Create(Reserva entity)` — cuerpo JSON.
Mezclar un archivo (`multipart/form-data`) en esa misma acción rompería esa firma. La
solución más simple es la que ya usa el patrón de `comprobantes-qr.md`: un segundo paso.
El formulario público llama primero a `POST /api/reservas` (como hoy, crea en `Pendiente`
y devuelve el `id`), y si el comensal adjuntó un archivo, hace un segundo llamado, también
anónimo, `POST /api/reservas/{id}/comprobante` (§6), con ese `id` recién recibido. Esto
agrega un segundo endpoint público al spec — el único que había hasta ahora era `Create`
(§2) — pero por una razón distinta a la que ese diseño original evitaba (listar mesas): acá
no se expone información, solo se recibe un archivo atado a una reserva que el propio
comensal acaba de crear.

**Guarda contra el id adivinable.** El `id` de `reserva` es un `bigint` secuencial,
adivinable. Para que nadie pueda colgarle un archivo a una reserva ajena, el endpoint de
comprobante solo acepta mientras `reserva.Estado = Pendiente` — una vez confirmada,
cancelada o cumplida, se rechaza con 409. Esto también resuelve, de paso, la pregunta de
hasta cuándo se puede adjuntar: solo antes de que el personal decida.

**Cancelar una reserva ya `Confirmada` — confirmado con el usuario el 2026-09-16.** Como al
confirmar ahora siempre nace un `Pedido`, cancelar una reserva `Confirmada` dejaría un
`Pedido` real huérfano de una reserva que ya no existe como tal — un estado que este spec
no sabe describir bien. Por eso **`Cancelada` deja de ser alcanzable desde `Confirmada`**;
la única transición válida desde `Confirmada` pasa a ser `Cumplida`. Si el cliente avisa
que no viene después de confirmada, el `Pedido` ya generado se gestiona por su propio
flujo (cerrarlo vacío, etc.), fuera de este spec.

**Riesgo heredado, no nuevo: `uk_pedido_comensal_activo`.**
`sql/003_pedido_comensal_unico.sql` ya exige que no haya dos pedidos activos
(`Abierto`/`EnPreparacion`) con el mismo `Comensal`. La conversión automática hereda ese
riesgo igual que cualquier alta de `Pedido` — si ya existe un pedido activo con el mismo
nombre, el `INSERT` falla con el 409 ya traducido por `TryTranslateDbError`, y la
transición `Pendiente` → `Confirmada` se rechaza entera (la reserva se queda `Pendiente`,
el personal ve el mensaje y decide). No es un caso nuevo que este spec introduzca — es el
mismo constraint que ya aplica hoy a cualquier `Pedido`, con un llamador más.

## 4. Esquema

`sql/016_reserva.sql` (numerado — es esquema nuevo, no carga de datos):

> **Corregido el 2026-09-09.** Este spec reclamaba `013`, número que ya ocupa
> `sql/013_mesa_compartida_por_turno.sql`, **corrido en producción**. La colisión la
> detectó el grafo de graphify al indexar los dos specs en la misma corrida
> ([SCRUM-27](https://caverop.atlassian.net/browse/SCRUM-27)). Con `014_rol_delivery.sql`
> aplicado y `015_cuenta_metodo_qr.sql` reservado por
> [reparacion-ck-cuenta-metodo.md](reparacion-ck-cuenta-metodo.md), el siguiente libre es
> `016`. **Verificar que siga libre antes de escribir el script**: este spec está
> propuesto y la numeración avanza con cada migración que se aplique mientras tanto.

```sql
CREATE TABLE reserva (
    id               bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_mesa          bigint      NOT NULL REFERENCES mesa(id),
    id_pedido        bigint      REFERENCES pedido(id),  -- null hasta Confirmada (§3.1)
    nombre_cliente   varchar(120) NOT NULL,
    telefono         varchar(30)  NOT NULL,
    fecha_hora       timestamptz NOT NULL,
    numero_personas  int         NOT NULL,
    estado           varchar(20) NOT NULL DEFAULT 'PENDIENTE',
    confirmada_en    timestamptz,
    cancelada_en     timestamptz,
    cumplida_en      timestamptz,
    creado_en        timestamptz NOT NULL DEFAULT now(),
    actualizado_en   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_reserva_personas CHECK (numero_personas > 0),
    CONSTRAINT ck_reserva_estado   CHECK (estado IN ('PENDIENTE','CONFIRMADA','CANCELADA','CUMPLIDA'))
);

CREATE TRIGGER tg_touch_reserva BEFORE UPDATE ON reserva
    FOR EACH ROW EXECUTE FUNCTION fn_touch();

-- Comprobante de pago/seña adjunto por el comensal al crear la reserva (§3.1).
-- Sin id_cuenta (no hay Cuenta en una reserva) y sin id_subido_por (sube el
-- comensal anonimo, no un usuario logueado) -- a proposito distinto de
-- comprobante_pago de comprobantes-qr.md, ver la razon en §3.1.
CREATE TABLE reserva_comprobante (
    id               bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_reserva       bigint      NOT NULL REFERENCES reserva(id),
    storage_key      text        NOT NULL,
    hash_sha256      char(64)    NOT NULL,
    tipo_contenido   varchar(30) NOT NULL,
    bytes            integer     NOT NULL,
    creado_en        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uk_reserva_comprobante_key  UNIQUE (storage_key),
    CONSTRAINT uk_reserva_comprobante_hash UNIQUE (id_reserva, hash_sha256),
    CONSTRAINT ck_reserva_comprobante_bytes CHECK (bytes > 0),
    CONSTRAINT ck_reserva_comprobante_tipo  CHECK (
        tipo_contenido IN ('image/webp','image/jpeg','image/png'))
);

CREATE INDEX ix_reserva_comprobante_reserva ON reserva_comprobante (id_reserva);

GRANT SELECT, INSERT ON reserva_comprobante TO app_restaurante;
```

Las dos tablas van en el mismo `sql/016_reserva.sql` — no ameritan un script separado,
nacen juntas.

`fn_touch()` ya existe (`sql/script_inicial.sql:349`) y es genérica — no hace falta escribir
otra. `confirmada_en`/`cancelada_en`/`cumplida_en` siguen la misma regla que
`ServidoEn`/`CerradoEn`/`PagadoEn` de `Pedido`/`Cuenta`: **el servidor las estampa**
(`DateTimeOffset.UtcNow`) al detectar la transición correspondiente, nunca las manda el
cliente — mismo motivo ya documentado en `CLAUDE.md` (Npgsql rechaza `DateTimeOffset` no-UTC
contra `timestamptz`, y un `InputDate` de navegador manda el offset local).

`fecha_hora` es distinto: es la fecha/hora **futura** que el comensal pide, no una marca de
"esto pasó ahora". La viene del comensal, no el servidor — pero como toda la app asume hora
de Bolivia en la interfaz (`ToBoliviaTime()`, UTC-4 fijo), el formulario público tiene que
convertir el valor elegido (hora de Bolivia) a UTC antes de mandarlo a la API, con el mismo
offset fijo, no `DateTime.Now`/offset del navegador.

## 5. Entidad, enum, configuración EF

- **`Atipico.Domain/Enums/EstadoReserva.cs`** — `Pendiente`, `Confirmada`, `Cancelada`,
  `Cumplida` (mismo estilo que `EstadoPedido`).
- **`Atipico.Domain/Entities/Reserva.cs`** — `IEntity`, más `IdMesa`/`Mesa` (navegación),
  `IdPedido`/`Pedido` (navegación, `long?`, null hasta `Confirmada` — §3.1),
  `NombreCliente`, `Telefono`, `FechaHora` (`DateTimeOffset`), `NumeroPersonas` (`int`),
  `Estado` (`EstadoReserva`), `ConfirmadaEn`/`CanceladaEn`/`CumplidaEn`
  (`DateTimeOffset?`), `CreadoEn`/`ActualizadoEn` (`DateTimeOffset`) — mismo patrón que
  `Plato`/`Pedido`.
- **`Atipico.Domain/Entities/ReservaComprobante.cs`** (nuevo, 2026-09-16) — `IEntity`, más
  `IdReserva`/`Reserva` (navegación), `StorageKey`, `HashSha256`, `TipoContenido`, `Bytes`
  (`int`), `CreadoEn` (`DateTimeOffset`) — mismo patrón reducido que `ComprobantePago`
  (§3.1 explica qué campos de ese modelo no aplican acá).
- **`Atipico.Infraestructure/Persistence/Configurations/ReservaConfiguration.cs`** —
  `IEntityTypeConfiguration<Reserva>`, mapeo `snake_case`, FK a `Mesa` y FK opcional a
  `Pedido`.
- **`Atipico.Infraestructure/Persistence/Configurations/ReservaComprobanteConfiguration.cs`**
  (nuevo) — `IEntityTypeConfiguration<ReservaComprobante>`, mapeo `snake_case`, FK a
  `Reserva`.
- **`AppDbContext`** — nuevo `DbSet<Reserva> Reservas` y
  `DbSet<ReservaComprobante> ReservaComprobantes`.

## 6. API y autorización

`Atipico.Api/Controllers/ReservasController.cs`, extiende `EntityControllerBase<Reserva>`
(mismo patrón que `PedidosController`: hereda lo genérico, sobrescribe lo que no calza).

`[Authorize]` ya está en `ApiControllerBase` a nivel de clase — un `[AllowAnonymous]` en la
acción puntual lo pisa sin afectar al resto del controlador (mismo mecanismo que ya usa
`AuthController.Login`). Así, `GetAll`/`GetById` (heredados, sin cambios) siguen exigiendo
login — es lo que le da al personal la lectura de "ver reservas" sin escribir nada nuevo.

```csharp
[Route("api/reservas")]
public class ReservasController : EntityControllerBase<Reserva>
{
    protected override string[] UpdateRoles => ["Admin", "Cajero"];
    // DeleteRoles se hereda ("Admin") pero nunca puede completarse: app_restaurante no
    // tiene DELETE. Igual que el resto de la app — no se anula acá porque una reserva ya
    // tiene su propio estado Cancelada para eso.

    [AllowAnonymous]
    [HttpPost]
    public override async Task<ActionResult<Reserva>> Create(Reserva entity)
    {
        // Fuerza Estado = Pendiente sin importar qué mande el cliente; valida
        // NumeroPersonas > 0 y FechaHora > UtcNow; busca la mesa más chica con capacidad
        // suficiente y estado <> Inactiva y la asigna a IdMesa; 409 con mensaje en español
        // si ninguna mesa entra. Delega a base.Create para el resto (incluida la
        // traducción de errores de base). El Id devuelto es el que el formulario público
        // usa para el segundo llamado de abajo, si el comensal adjuntó comprobante.
    }

    [AllowAnonymous]
    [HttpPost("{id:long}/comprobante")]
    public async Task<IActionResult> AdjuntarComprobante(long id, IFormFile archivo)
    {
        // Nuevo, 2026-09-16 (§3.1). Segundo endpoint anónimo del spec. 404 si la reserva
        // no existe; 409 si Estado <> Pendiente (guarda contra el id adivinable, §3.1).
        // Mismo pipeline de procesamiento que ComprobantesController: magic bytes, quita
        // EXIF, recodifica a WebP, calcula SHA-256, sube a R2, INSERT en
        // reserva_comprobante — pero sin IdSubidoPor (sube el comensal anónimo).
    }

    [HttpGet("{id:long}/comprobante/url")]
    public async Task<ActionResult<string>> UrlComprobante(long id)
    {
        // Nuevo, 2026-09-16. Detrás de login (mismo nivel que GetAll: cualquier rol).
        // URL firmada de 5 minutos, mismo patrón que ComprobantesController.UrlAsync —
        // para que el personal lo revise antes de confirmar (§7).
    }

    [HttpPut("{id:long}")]
    public override async Task<IActionResult> Update(long id, Reserva entity)
    {
        // Igual que PedidosController/CuentasController: trae la entidad rastreada,
        // valida la transición de Estado (Pendiente->Confirmada->Cumplida,
        // Pendiente->Cancelada; Confirmada->Cancelada YA NO es válida desde el
        // 2026-09-16 — ver la decisión abierta en §3.1 —; cualquier otro salto se
        // rechaza), y estampa ConfirmadaEn/CanceladaEn/CumplidaEn según corresponda.
        //
        // Pendiente->Confirmada: pone mesa.Estado = Reservada (409 si ya estaba
        // Reservada u Ocupada), Y ADEMÁS crea el Pedido (§3.1: Estado=Abierto,
        // IdMesa=entity.IdMesa, IdMesero=UsuarioActualId, Comensal=NombreCliente,
        // TipoPedido=EnSalon) envuelto en el mismo try/catch DbUpdateException ->
        // TryTranslateDbError que el resto de la app (uk_pedido_comensal_activo puede
        // rechazarlo, §3.1) y estampa reserva.IdPedido con el id generado.
        //
        // Pendiente->Cancelada: si la mesa seguía Reservada por esta reserva, vuelve a
        // Libre.
        //
        // Confirmada->Cumplida: mesa.Estado = Ocupada (ya no vuelve a Libre — §3.1).
    }
}
```

`Create` y el nuevo `AdjuntarComprobante` son los **dos** endpoints anónimos del spec —
confirmar/cancelar/cumplir, y ahora también ver la URL del comprobante, siguen detrás de
login, con `Admin`/`Cajero` (no `Mesero`: gestiona pedidos, no reservas; sí puede **ver**
la lista y el comprobante, porque `GetAll`/`UrlComprobante` solo exigen estar logueado con
cualquier rol, igual que el resto de la app).

## 7. Web

- **`Atipico.Web/Components/Pages/Reservar.razor`** — página pública, **sin**
  `[Authorize]` (la única otra página así hoy es `Login.razor`). Pide nombre, teléfono,
  fecha/hora, número de personas, y un selector de archivo **opcional** para el
  comprobante (§3.1). Sin sesión, así que llama a la API con un `HttpClient` nombrado sin
  el `JwtForwardingHandler` (no hay JWT que reenviar) — no puede usar
  `IEntityApiClient<Reserva>` porque ese cliente asume un usuario logueado detrás, igual
  que el login de hoy no usa el cliente genérico. **Flujo de dos pasos** (2026-09-16): al
  enviar, primero `POST /api/reservas` (como siempre); si el comensal adjuntó un archivo,
  con el `id` devuelto hace un segundo `POST /api/reservas/{id}/comprobante` (multipart).
  Igual que `comprobantes-qr.md` §8.1: **adjuntar es opcional y nunca bloquea** — si el
  segundo llamado falla, la reserva ya existe y se avisa del fallo de subida por separado,
  no se revierte la reserva.
- **`Atipico.Web/Components/Pages/Reservas/Index.razor`** — interna, `[Authorize]`,
  lista con `EntityTable`, columnas mesa/cliente/teléfono/fecha/personas/estado, botones de
  confirmar/cancelar/cumplir según el estado actual de cada fila (no un único botón
  "editar" — son transiciones, como `PedidoPlatos/Edit.razor`). **Sin botón nuevo de
  conversión** — la conversión a `Pedido` es automática al confirmar (§3.1), no una acción
  aparte. Agrega (2026-09-16): un enlace "Ver comprobante" cuando la reserva tiene uno
  (via `GET /api/reservas/{id}/comprobante/url`, §6) para que el personal lo revise
  **antes** de confirmar; y, una vez `Confirmada`, un enlace "Ver pedido" hacia
  `Pedidos/Edit.razor?id={reserva.IdPedido}`.
- **`Atipico.Web/Services/ApiRoutes.cs`** — registrar la ruta de `Reserva`.
- **Navegación**: nuevo link en `Navegacion.cs`, sección visible para `Admin`/`Cajero`
  (`RolesCsv` se deriva solo, no se declara — ver `CLAUDE.md`).

## 8. Criterios de aceptación

- [ ] CA-1 — Un `POST /api/reservas` sin token (anónimo) con datos válidos crea una
  reserva en estado `Pendiente` y asigna una mesa con capacidad suficiente.
- [ ] CA-2 — Ese mismo `POST` sin ninguna mesa con capacidad suficiente (o todas
  `Inactiva`) devuelve 409 con mensaje en español, sin crear la fila.
- [ ] CA-3 — `PUT` de `Pendiente` a `Confirmada` estampa `ConfirmadaEn`, pone la mesa en
  `RESERVADA` (falla con 409 si la mesa ya estaba `RESERVADA`/`OCUPADA`), **y crea un
  `Pedido`** (`Estado=Abierto`, `IdMesa` de la reserva, `Comensal=NombreCliente`) cuyo id
  queda en `reserva.IdPedido`. *(agregado 2026-09-16, §3.1)*
- [ ] CA-3b — Si ya existe un pedido activo con el mismo `Comensal`
  (`uk_pedido_comensal_activo`), la transición `Pendiente`→`Confirmada` se rechaza con 409
  traducido, sin dejar la reserva a medio confirmar. *(nuevo, §3.1)*
- [ ] CA-4 — `PUT` de `Confirmada` a `Cumplida` estampa `CumplidaEn` y pasa la mesa a
  `OCUPADA` (ya no a `LIBRE` — *corregido 2026-09-16, ver bitácora*).
- [ ] CA-5 — `PUT` a `Cancelada` **solo desde `Pendiente`** (ya no desde `Confirmada` —
  *corregido y confirmado con el usuario 2026-09-16, §3.1*) estampa `CanceladaEn` y libera
  la mesa si correspondía.
- [ ] CA-6 — Cualquier otro salto de estado (`Pendiente`→`Cumplida` directo,
  `Confirmada`→`Cancelada`, `Cancelada`→cualquier otro) se rechaza con 400/409, no con 500.
- [ ] CA-7 — `GET /api/reservas` sin token devuelve 401 — la lectura interna sigue
  protegida aunque la creación sea pública.
- [ ] CA-8 — La página `/reservar` es alcanzable sin sesión iniciada.
- [ ] CA-9 — `POST /api/reservas/{id}/comprobante` sin token, con la reserva en
  `Pendiente` y un archivo válido, registra una fila en `reserva_comprobante` y sube el
  objeto a R2. *(nuevo, §3.1)*
- [ ] CA-10 — Ese mismo `POST` sobre una reserva que ya no está `Pendiente`
  (`Confirmada`/`Cancelada`/`Cumplida`) devuelve 409, sin registrar nada. *(nuevo, §3.1 —
  guarda contra el id adivinable)*
- [ ] CA-11 — `GET /api/reservas/{id}/comprobante/url` sin token devuelve 401 — verlo
  sigue detrás de login aunque subirlo sea público. *(nuevo)*

## 9. Plan

1. **Spec** — este documento. *(hecho)*
2. **Aprobación** — pendiente. No se escribe código hasta que el usuario lo apruebe.
3. **Pruebas** — agente `qa`: casos de CA-1 a CA-11 sobre `ReservasController`
   (`Atipico.Api.Tests`), en rojo.
4. **Implementación** — agente `dev`: esquema (`reserva` + `reserva_comprobante`),
   entidad, configuración EF, controlador (reutiliza el adaptador de almacenamiento de
   `comprobantes-qr.md` para el comprobante de reserva), páginas Web. Pone las pruebas en
   verde.
5. **Verificación en pantalla** — la hace el usuario.

## Bitácora

- **2026-09-08** — SCRUM-21 no tenía descripción. Se preguntó directamente quién crea la
  reserva, si aparta una mesa específica, y qué estados tiene — las tres decisiones de §1.
  El resto del diseño (auto-asignación de mesa en vez de que el comensal elija, cuándo se
  bloquea la mesa, qué endpoints quedan anónimos) se derivó de esas tres respuestas y se
  documenta con su razón en §2-§3, sin volver a preguntar cada detalle.
- **Se descartó** dejar que el comensal eligiera la mesa de una lista: expondría un segundo
  endpoint anónimo solo para listar mesas, y un cliente público no tiene por qué conocer la
  numeración del local. Auto-asignar por capacidad cumple igual la decisión de "mesa
  específica" (la fila nace con `id_mesa`, no nulo) sin ese costo.
- **2026-09-16** — Reconciliación con SCRUM-21: el usuario pidió "el comensal realiza un
  pedido y puede adjuntar comprobante, pero es el mesero o cajero quien convierte esa
  reserva en pedido" — frase internamente ambigua (usaba "pedido" y "reserva" para lo
  mismo). Antes de tocar este documento se confirmaron con el usuario, vía
  `AskUserQuestion` (terminaron siendo 5 preguntas, una sub-pregunta se abrió sola): (1) es
  un lapsus, sigue siendo `Reserva`, no cambia quién crea qué; (2) el comprobante es
  evidencia de pago/seña, no puede reusar `comprobante_pago` porque esa tabla exige
  `Cuenta` NOT NULL y una reserva no tiene cuenta; (3) la conversión a `Pedido` dispara al
  confirmar (`Pendiente`→`Confirmada`), no al cumplir, y la mesa se queda `RESERVADA` en
  ese momento — no `OCUPADA` todavía; (4) la mesa pasa a `OCUPADA` recién al cumplir
  (`Confirmada`→`Cumplida`), porque ahí el cliente ya está físicamente en el local. Si es
  obligatorio o no el comprobante no se preguntó explícitamente — se documentó como
  decisión derivada (opcional) en §1.1, no como respuesta del usuario.
  - Cambios concretos: nueva §1.1 (tabla de decisiones ampliadas) y nueva §3.1 (mecánica
    de conversión, comprobante, y por qué no reusa `comprobante_pago`); se reescribió el
    párrafo de §3 que ya no era cierto (`Cumplida` no tocaba la mesa — ahora sí, y
    `Confirmada` pasó de solo reservar la mesa a también crear el `Pedido`); esquema
    ampliado en §4 (`reserva.id_pedido`, tabla `reserva_comprobante`); entidad/config
    nuevas en §5; dos endpoints nuevos en §6 (`AdjuntarComprobante` anónimo,
    `UrlComprobante` protegido) y la lógica de `Update` reescrita; `Reservar.razor` y
    `Reservas/Index.razor` actualizados en §7; CA-3, CA-4, CA-5, CA-6 corregidos y CA-3b,
    CA-9, CA-10, CA-11 agregados en §8.
  - Se agregó también el frontmatter YAML de `specs/formato-spec.md` §4.1, que este
    documento no tenía — se aprovechó el mismo turno de edición para adoptarlo (§10.1 de
    ese spec: "un spec viejo adopta el formato cuando se lo toca por otra razón").
  - Quedó una pregunta sin cerrar en esa primera pasada: qué pasa si el personal quiere
    cancelar una reserva ya `Confirmada` (con `Pedido` ya generado). Se preguntó aparte, por
    `AskUserQuestion`: el usuario confirmó que **no** debe seguir siendo alcanzable — el
    default que ya había quedado escrito en §3.1/CA-5 queda cerrado tal cual, no era
    provisorio.
