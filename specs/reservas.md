# Reservas de mesa por el comensal

Especificación para SCRUM-21 ("Crear formulario para reservas").

- **Estado:** **propuesto**, pendiente de aprobación.
- **Alcance:** un formulario público (sin login) para que el comensal reserve una mesa, y
  una pantalla interna para que el personal confirme, cancele o cierre esas reservas.
- **Fuera de alcance:** notificaciones al comensal (email/SMS) cuando se confirma o
  cancela; que el comensal pueda ver, cambiar o cancelar su propia reserva después de
  crearla; detección de solapamiento por duración (la reserva no tiene un "hasta", solo un
  instante); un plano de mesas visual. Todo esto queda anotado en §7 para un ciclo próximo,
  no se diseña acá.

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

## 2. Por qué el comensal no elige la mesa a mano

"Mesa específica" significa que la fila de `reserva` tiene un `id_mesa` concreto desde que
se crea — no que el comensal navegue una lista de números de mesa. Un cliente público no
conoce el plano del local ni le importa qué mesa es la 7. Por eso: el comensal solo indica
**cuántas personas** y **cuándo**; el sistema **asigna automáticamente** la mesa más chica
que le entra (`capacidad >= numero_personas`, `estado <> 'INACTIVA'`), y responde con el
número de mesa asignado como confirmación de la solicitud.

Esto también evita tener que exponer un segundo endpoint anónimo (listar mesas) — el único
endpoint público de todo este spec es el de crear la reserva. Si más adelante se quiere
que el comensal vea el plano y elija, es un cambio de UI sobre el mismo endpoint (recibir
`id_mesa` opcional en vez de calcularlo), no un cambio de esquema.

## 3. Por qué "Pendiente" no bloquea la mesa

Si crear la reserva (público, sin revisión) ya pusiera la mesa en `RESERVADA`, cualquiera
podría bloquear mesas del local sin que el personal se entere. Por eso:

- **Al crear** (siempre `Pendiente`): se **valida** que exista una mesa con capacidad
  suficiente y no `INACTIVA`, pero **no se cambia su estado**. Pueden existir varias
  reservas `Pendiente` para la misma mesa/horario a la vez — son solicitudes, no promesas.
- **Al confirmar** (`Pendiente` → `Confirmada`, hace el personal): recién ahí la mesa pasa a
  `RESERVADA`. Se rechaza si la mesa ya está `RESERVADA` u `OCUPADA` en ese momento — el
  personal ve el conflicto y decide (confirma otra mesa a mano, o cancela alguna de las
  solicitudes en conflicto).
- **Al cancelar** (`Pendiente`/`Confirmada` → `Cancelada`) o **cerrar** (`Confirmada` →
  `Cumplida`, el cliente llegó): si la mesa estaba `RESERVADA` por esta reserva, vuelve a
  `LIBRE`. `Cumplida` no la pasa a `OCUPADA` — eso lo maneja el flujo de `Pedido` que ya
  existe, cuando el mesero abre el pedido en esa mesa.

Es una simplificación consciente: no hay ventana horaria ("de 20:00 a 22:00"), solo un
instante. Comparar "mesa ya reservada" es mirar su `estado` actual, no calcular
solapamientos — ver §7.

## 4. Esquema

`sql/013_reserva.sql` (numerado — es esquema nuevo, no carga de datos):

```sql
CREATE TABLE reserva (
    id               bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_mesa          bigint      NOT NULL REFERENCES mesa(id),
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
```

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
  `NombreCliente`, `Telefono`, `FechaHora` (`DateTimeOffset`), `NumeroPersonas` (`int`),
  `Estado` (`EstadoReserva`), `ConfirmadaEn`/`CanceladaEn`/`CumplidaEn`
  (`DateTimeOffset?`), `CreadoEn`/`ActualizadoEn` (`DateTimeOffset`) — mismo patrón que
  `Plato`/`Pedido`.
- **`Atipico.Infraestructure/Persistence/Configurations/ReservaConfiguration.cs`** —
  `IEntityTypeConfiguration<Reserva>`, mapeo `snake_case`, FK a `Mesa`.
- **`AppDbContext`** — nuevo `DbSet<Reserva> Reservas`.

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
        // traducción de errores de base).
    }

    [HttpPut("{id:long}")]
    public override async Task<IActionResult> Update(long id, Reserva entity)
    {
        // Igual que PedidosController/CuentasController: trae la entidad rastreada,
        // valida la transición de Estado (Pendiente->Confirmada->Cumplida,
        // Pendiente/Confirmada->Cancelada; cualquier otro salto se rechaza), estampa
        // ConfirmadaEn/CanceladaEn/CumplidaEn según corresponda, y actualiza mesa.Estado
        // (Confirmada: pone RESERVADA si la mesa está libre, o 409 si no; Cancelada/
        // Cumplida: si la mesa seguía RESERVADA por esta reserva, vuelve a LIBRE).
    }
}
```

`Create` es el **único** endpoint anónimo de todo el spec — confirmar/cancelar/cumplir
siguen detrás de login, con `Admin`/`Cajero` (no `Mesero`: gestiona pedidos, no reservas;
sí puede **ver** la lista, porque `GetAll` solo exige estar logueado con cualquier rol,
igual que el resto de la app).

## 7. Web

- **`Atipico.Web/Components/Pages/Reservar.razor`** — página pública, **sin**
  `[Authorize]` (la única otra página así hoy es `Login.razor`). Pide nombre, teléfono,
  fecha/hora, número de personas. Sin sesión, así que llama a la API con un
  `HttpClient` nombrado sin el `JwtForwardingHandler` (no hay JWT que reenviar) — no puede
  usar `IEntityApiClient<Reserva>` porque ese cliente asume un usuario logueado detrás,
  igual que el login de hoy no usa el cliente genérico.
- **`Atipico.Web/Components/Pages/Reservas/Index.razor`** — interna, `[Authorize]`,
  lista con `EntityTable`, columnas mesa/cliente/teléfono/fecha/personas/estado, botones de
  confirmar/cancelar/cumplir según el estado actual de cada fila (no un único botón
  "editar" — son transiciones, como `PedidoPlatos/Edit.razor`).
- **`Atipico.Web/Services/ApiRoutes.cs`** — registrar la ruta de `Reserva`.
- **Navegación**: nuevo link en `Navegacion.cs`, sección visible para `Admin`/`Cajero`
  (`RolesCsv` se deriva solo, no se declara — ver `CLAUDE.md`).

## 8. Criterios de aceptación

- [ ] CA-1 — Un `POST /api/reservas` sin token (anónimo) con datos válidos crea una
  reserva en estado `Pendiente` y asigna una mesa con capacidad suficiente.
- [ ] CA-2 — Ese mismo `POST` sin ninguna mesa con capacidad suficiente (o todas
  `Inactiva`) devuelve 409 con mensaje en español, sin crear la fila.
- [ ] CA-3 — `PUT` de `Pendiente` a `Confirmada` estampa `ConfirmadaEn`, pone la mesa en
  `RESERVADA`, y falla con 409 si la mesa ya estaba `RESERVADA`/`OCUPADA`.
- [ ] CA-4 — `PUT` de `Confirmada` a `Cumplida` estampa `CumplidaEn` y libera la mesa a
  `LIBRE` si seguía `RESERVADA` por esta reserva.
- [ ] CA-5 — `PUT` a `Cancelada` desde `Pendiente` o `Confirmada` estampa `CanceladaEn` y
  libera la mesa si correspondía.
- [ ] CA-6 — Cualquier otro salto de estado (`Pendiente`→`Cumplida` directo,
  `Cancelada`→cualquier otro) se rechaza con 400/409, no con 500.
- [ ] CA-7 — `GET /api/reservas` sin token devuelve 401 — la lectura interna sigue
  protegida aunque la creación sea pública.
- [ ] CA-8 — La página `/reservar` es alcanzable sin sesión iniciada.

## 9. Plan

1. **Spec** — este documento. *(hecho)*
2. **Aprobación** — pendiente. No se escribe código hasta que el usuario lo apruebe.
3. **Pruebas** — agente `qa`: casos de CA-1 a CA-7 sobre `ReservasController`
   (`Atipico.Api.Tests`), en rojo.
4. **Implementación** — agente `dev`: esquema, entidad, configuración EF, controlador,
   páginas Web. Pone las pruebas en verde.
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
