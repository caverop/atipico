# Una mesa en más de un pedido del mismo turno

Especificación funcional y técnica para SCRUM-17 (Jira, proyecto `atipico`): "Permitir que una
mesa sea ocupada en mas de un pedido en un mismo turno". Documento de referencia previo a la
implementación: recoge las decisiones tomadas y su porqué, para no volver a discutirlas al
escribir el código.

- **Estado:** **propuesto**. Sin código todavía.
- **Origen:** [SCRUM-17](https://caverop.atlassian.net/browse/SCRUM-17), tipo Task, sin
  descripción ni criterios de aceptación en el ticket — el alcance de este documento es la
  interpretación de lo pedido, hecha al analizarlo.
- **Es una reversión, no una funcionalidad nueva.** Este ticket deshace, a propósito, la mitad
  de una regla de negocio que `specs/numero-pedido.md` §4.10 documenta como **RN-14**, agregada
  en `sql/012_pedido_unicidad_por_turno.sql`. Ese spec queda como registro de lo que se decidió
  entonces y por qué; este documento registra por qué se relaja ahora, sin reescribir aquel.
- **Alcance:** que dos (o más) pedidos activos del mismo turno puedan tener asociada la misma
  mesa a la vez.
- **Fuera de alcance:** la regla hermana de RN-13 (comensal único por turno) **no se toca** —
  el ticket habla solo de mesa. Tampoco se agrega ningún tope (ej. "máximo N pedidos por
  mesa"): se vuelve exactamente al comportamiento anterior a `012`, sin agregar una regla
  nueva en su lugar.

---

## 1. Contexto: la regla que se relaja, y por qué existía

`sql/012_pedido_unicidad_por_turno.sql` agregó `fn_pedido_mesa_ocupada` /
`tg_pedido_mesa_ocupada`, un trigger `BEFORE INSERT OR UPDATE ON pedido_mesa` que rechaza
asociar una mesa a un pedido si esa mesa ya está en otro pedido **vivo** (no `CERRADO` ni
`ANULADO`) del mismo turno:

```sql
RAISE EXCEPTION 'La mesa % ya está ocupada por otro pedido de este turno.', v_numero;
```

El motivo documentado entonces (`specs/numero-pedido.md` §4.10) era cerrar un agujero real:
antes de `012`, "dos pedidos distintos podían sentarse en la mesa 5 a la vez y la interfaz se
limitaba a avisar 'ya ocupada por otro pedido' dejando pasar igual" — es decir, evitar que se
sentara accidentalmente a dos grupos distintos sobre la misma mesa sin que nadie lo notara.

**El caso que el trigger bloquea de más es real y ya estaba anticipado en el propio código**,
sin que nadie lo arreglara: `Pedidos/Edit.razor`, en `_mesasDisponibles`, todavía trae el
comentario original (anterior a `012`, nunca actualizado):

> "Dos o mas pedidos pueden compartir una mesa (p.ej. grupos distintos en una mesa grande),
> por eso no se excluyen las mesas Ocupadas"

Ese comentario le miente a quien lo lea: hoy, pese a que `_mesasDisponibles` no excluye las
mesas `Ocupada`, el `<select>` las lista **deshabilitadas** (`disabled="@(mesa.Estado ==
EstadoMesa.Ocupada)"`) precisamente porque el trigger las rechazaría, y otro comentario en el
mismo archivo lo dice sin rodeos: *"Una mesa Ocupada la rechaza tg_pedido_mesa_ocupada, asi que
no sirve ni como opcion ni como valor por defecto."* Dos comentarios contradictorios en el
mismo archivo, sobrevivientes de una regla que cambió de sentido y de la otra que no se
actualizó. Este ticket resuelve la contradicción a favor del primero: mesas grandes compartidas
por grupos distintos es un caso de uso legítimo, y la interfaz ya había sido pensada para
soportarlo antes de que `012` lo bloqueara sin querer de forma más amplia de lo que su propio
motivo (evitar el doble-sentado *accidental*) exigía.

## 2. Decisión: eliminar el trigger, no acotarlo

Alternativa evaluada: mantener el trigger pero con una excepción (por ejemplo, permitir
compartir solo si el mesero confirma, o limitar a N pedidos por mesa). Se descarta: el ticket
pide volver a permitirlo lisa y llanamente, `Mesa.Estado` (Libre/Ocupada) ya modela
correctamente "¿hay al menos un pedido activo usando esta mesa?" sin asumir que es uno solo
(ver §3), y agregar un tope o una confirmación sería una regla nueva que nadie pidió — el
mismo error de alcance que ya le costó a esta regla haber bloqueado de más en primer lugar.

Se elimina el trigger completo (`DROP TRIGGER` + `DROP FUNCTION`) en una migración nueva. La
migración `012` **no se edita** — las migraciones numeradas y aplicadas no se tocan; el cambio
va en una nueva, `013`.

## 3. Por qué no hace falta tocar `Mesa.Estado`

`Mesa.Estado` (`Libre`/`Ocupada`/`Reservada`/`Inactiva`) ya está escrito de forma genérica en
`PedidoMesasController` y en `PedidosController.LiberarMesaSiNoTieneOtroPedidoActivoAsync`:
ninguno de los dos asume que una mesa `Ocupada` tiene exactamente un pedido activo.

- **Al asociar** (`PedidoMesasController.Create`): `Libre` → `Ocupada` solo si la mesa estaba
  `Libre`. Si ya estaba `Ocupada` (por el pedido que se va a permitir compartir), se queda
  igual — no hay una transición que romper.
- **Al liberar** (`LiberarMesaSiNoTieneOtroPedidoActivoAsync`, llamada desde
  `PedidoMesasController.Delete` y desde `PedidosController.Update` al cerrar/anular): ya
  cuenta *todos* los pedidos con esa mesa y solo libera si **ninguno** sigue activo — esto es
  exactamente lo correcto cuando hay dos o más pedidos compartiéndola: la mesa sigue `Ocupada`
  mientras quede uno vivo, y recién se libera cuando cierra el último.

Es decir: el modelo de estado de `Mesa` ya fue escrito pensando en N pedidos por mesa (aunque
el trigger de `012` después lo redujera a 1 en la práctica). No hay cambio de Domain ni de
Infraestructura que hacer.

## 4. SQL

```sql
-- sql/013_mesa_compartida_por_turno.sql
BEGIN;

-- SCRUM-17: revierte la mitad de sql/012_pedido_unicidad_por_turno.sql que trataba a la mesa
-- (RN-14, specs/numero-pedido.md §4.10). La regla hermana sobre el comensal (RN-13,
-- uk_pedido_comensal_activo) NO se toca: el ticket pide solo la mesa. Ver
-- specs/mesa-compartida-por-turno.md.
--
-- El caso que bloqueaba de más: dos pedidos activos (p.ej. dos grupos distintos, o platos
-- pedidos en momentos separados) sentados sobre la misma mesa grande a la vez. Es un uso
-- legítimo del salón, y Mesa.Estado ya lo modela bien sin asumir un único pedido activo por
-- mesa (ver specs/mesa-compartida-por-turno.md §3): no hace falta reemplazar la regla por
-- otra, alcanza con quitarla.
DROP TRIGGER tg_pedido_mesa_ocupada ON pedido_mesa;
DROP FUNCTION fn_pedido_mesa_ocupada();

COMMIT;
```

**No hay `GRANT` que tocar**: eliminar un trigger no cambia los permisos de `app_restaurante`
sobre `pedido_mesa`.

**`uk_pedido_mesa` sigue igual.** Ese índice (`PedidoMesaConfiguration.cs`, único sobre
`(id_pedido, id_mesa)`) impide asociar la misma mesa dos veces **al mismo** pedido — una
restricción de integridad distinta, que nunca tuvo que ver con RN-14 y que este ticket no
toca.

## 5. Interfaz: `Pedidos/Edit.razor`

Hoy, en la sección "Mesas del pedido":

```csharp
private List<Mesa> _mesasLibres =>
    _mesasDisponibles.Where(m => m.Estado != EstadoMesa.Ocupada).ToList();

private long _mesaPorDefecto => _mesasLibres.FirstOrDefault()?.Id ?? 0;
```

```razor
<option value="@mesa.Id" disabled="@(mesa.Estado == EstadoMesa.Ocupada)">
    Mesa @mesa.Numero (cap. @mesa.Capacidad)@(mesa.Estado == EstadoMesa.Ocupada ? " - ocupada por otro pedido" : "")
</option>
```

y el bloque condicional que decide qué mensaje mostrar según haya o no mesas libres:

```razor
@if (_mesasLibres.Count > 0) { /* selector */ }
else if (_mesasDisponibles.Count > 0) { <p>Todas las mesas están ocupadas por otros pedidos de este turno.</p> }
else { <p>No hay mesas disponibles para asociar.</p> }
```

Cambia a:

- **La opción deja de estar `disabled`.** Una mesa `Ocupada` es una opción válida de nuevo.
- **El texto informa, no advierte de un rechazo.** "- ocupada por otro pedido" describe un
  hecho que ya no impide la acción; se mantiene como información (para que el mesero sepa que
  la va a compartir, y con qué transparencia — ver más abajo) pero ya no acompaña una opción
  deshabilitada.
- **`_mesaPorDefecto` prefiere una mesa libre, pero ya no se resigna a `0` si no hay ninguna.**
  Con todas las mesas `Ocupada`, el valor por defecto pasa a ser la primera de
  `_mesasDisponibles` (antes, antes de `012`, era exactamente este comportamiento — ver el
  comentario ya presente en el código: *"antes, con todas las mesas tomadas, el select
  arrancaba sobre una que el servidor iba a rechazar"*, que ahora deja de aplicar porque el
  servidor ya no la rechaza):
  ```csharp
  private long _mesaPorDefecto => _mesasDisponibles.FirstOrDefault()?.Id ?? 0;
  ```
  `_mesasLibres` dejaría de tener un segundo propósito (ya no filtra opciones del `<select>`,
  solo decidiría el orden de preferencia del valor por defecto) — se evalúa al implementar si
  conviene conservarlo con ese único uso o resolverlo con un `OrderBy` sobre
  `_mesasDisponibles` directamente; no cambia el comportamiento observable.
- **El tercer mensaje ("Todas las mesas están ocupadas...") deja de ser el camino normal.**
  Con la opción habilitada, ese `else if` solo se alcanza si además se quiere seguir avisando
  que no queda ninguna libre — a decidir en la implementación si vale la pena conservarlo como
  aviso informativo (no bloqueante) junto al selector, en vez de reemplazar al selector.
- **Sin diálogo de confirmación.** "Agregar mesa" sobre una mesa libre no pide confirmar hoy;
  compartir una mesa tampoco la va a pedir — es una acción del mismo tipo, no una más
  riesgosa que amerite un paso extra. El texto informativo junto a la opción ya cumple el
  papel de que el mesero vea lo que está haciendo antes de elegirlo.

Los dos comentarios contradictorios de §1 se corrigen en el mismo cambio: el que describe el
trigger como bloqueante se actualiza para reflejar que ya no lo es, y el que ya explicaba el
"por qué compartir es válido" queda, por fin, siendo cierto otra vez.

## 6. Qué NO cambia

- `Mesas/Index.razor` y `MesasController`: sin cambios — el CRUD de mesas es indiferente a
  cuántos pedidos las usan.
- `PedidoMesasController`: sin cambios de código — su lógica de `Estado` ya era correcta para
  N pedidos por mesa (§3); lo único que cambiaba el resultado era el trigger de la base, ajeno
  al controlador.
- RN-13 / `uk_pedido_comensal_activo` (comensal único por turno): sin cambios.
- La grilla de pedidos (`specs/numero-mesa-grilla-pedidos.md`, SCRUM-16): la columna "Mesa" ya
  soporta mostrar varias mesas por pedido; no soportaba (ni necesita soportar) mostrar varios
  *pedidos* por mesa, porque la grilla lista pedidos, no mesas. Sin cambios ahí.

## 7. Plan de implementación

| # | Capa | Archivo | Cambio |
|---|---|---|---|
| 1 | SQL | `sql/013_mesa_compartida_por_turno.sql` | `DROP TRIGGER`/`DROP FUNCTION` (§4) |
| 2 | Web | `Components/Pages/Pedidos/Edit.razor` | quitar `disabled`, ajustar `_mesaPorDefecto`, actualizar comentarios y mensajes (§5) |
| 3 | Specs | `specs/numero-pedido.md` | anotar RN-14 y §4.10 como relajadas por este documento, sin reescribir el registro histórico |
| 4 | Pruebas | ver §8 | — |

No hay cambios de Domain, Infraestructura ni Api más allá del SQL: confirmado en §3 que
`Mesa.Estado` y los controladores ya estaban escritos para soportar esto.

## 8. Plan de pruebas

`Atipico.Infraestructure.Tests/ModeloTurnoCajaTest.cs` tiene
`LaMesaOcupadaLaRechazaUnTriggerEnInsertYEnUpdate`, que lee `012_pedido_unicidad_por_turno.sql`
y verifica que **ese archivo** define el trigger. Sigue siendo cierto — `012` no se edita — así
que **ese test no cambia y sigue en verde**; documenta lo que la base hacía en ese momento de
su historia, no lo que hace hoy.

Casos nuevos a agregar:

1. **Un test simétrico sobre `013`**, en el mismo archivo o uno nuevo
   (`Atipico.Infraestructure.Tests`), que lea `sql/013_mesa_compartida_por_turno.sql` y
   verifique que contiene `DROP TRIGGER tg_pedido_mesa_ocupada` y
   `DROP FUNCTION fn_pedido_mesa_ocupada` — mismo patrón de "leer el script, no reinterpretarlo"
   que ya usa el resto de la suite para migraciones (`ModeloTipoPedidoTest`,
   `ModeloTurnoCajaTest`).
2. **Ningún test unitario de `PedidosController`/`PedidoMesasController` necesita cambiar**: el
   trigger vive en la base y estos tests mockean `IEntityService<T>`, nunca la ejecutan. No hay
   una prueba unitaria hoy que afirme "una mesa ocupada es rechazada" (esa propiedad solo
   estaba verificada contra Postgres real, según el callout de `specs/numero-pedido.md` §4.10)
   y por lo tanto no hay ninguna que quede en rojo por este cambio.
3. **Verificación manual/empírica pendiente** (igual que se hizo para `012`, ver
   `specs/numero-pedido.md` §13.3): correr `013` contra una base con el esquema completo y
   confirmar que un segundo `INSERT` en `pedido_mesa` sobre una mesa ya ocupada por otro
   pedido vivo del mismo turno **ya no** lanza `P0001`, y que al cerrar/anular uno de los dos
   pedidos la mesa sigue `Ocupada` hasta que cierra el último (esto ya lo cubre
   `LiberarMesaSiNoTieneOtroPedidoActivoAsync`, pero vale confirmarlo en la base real una vez
   corrida la migración).
4. Si al implementar §5 se detecta necesario cubrir `Pedidos/Edit.razor` con un test de
   componente (bUnit, como `Atipico.Web.Tests/PedidosIndexMesaTests.cs` de SCRUM-16), el caso a
   cubrir es: con una mesa `Ocupada` en `_mesas` y ninguna `Libre`, la opción aparece habilitada
   y seleccionable, y `HandleAddMesa` la asocia.

## 9. Puesta en producción

1. Correr `sql/013_mesa_compartida_por_turno.sql` contra la base de producción. Es una sola
   transacción, no reescribe ninguna tabla ni bloquea filas más allá del `DROP`.
2. No hay backfill ni dato que migrar: el cambio es de comportamiento hacia adelante.
3. No hay variables de entorno ni configuración nueva.

## 10. Bitácora

- **2026-09-05.** Análisis inicial a partir de SCRUM-17 (sin descripción en el ticket). Se
  identificó que el ticket pide revertir la mitad de RN-14 (`specs/numero-pedido.md` §4.10,
  `sql/012_pedido_unicidad_por_turno.sql`), y que el propio código ya tenía un comentario en
  `Pedidos/Edit.razor` (`_mesasDisponibles`) anticipando este caso de uso ("grupos distintos en
  una mesa grande") que quedó contradicho, sin actualizarse, cuando `012` bloqueó de más. Se
  verificó que `Mesa.Estado` y los controladores ya estaban escritos de forma genérica para N
  pedidos por mesa, así que el cambio se acota a SQL (`DROP TRIGGER`/`DROP FUNCTION`) más UI.
  Pendiente de aprobación antes de tocar código.
