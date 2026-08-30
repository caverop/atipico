# Número de pedido por turno

**Estado:** implementado — falta correr los scripts en la base y probarlo en pantalla
**Afecta:** `sql/`, `Atipico.Domain`, `Atipico.Infraestructure`, `Atipico.Api`, `Atipico.Web`

**Contenido:** §1–§11 el diseño · §12 el plan de acción y lo que cambió al ejecutarlo · §13 la bitácora

---

## 1. Problema

La grilla de pedidos identifica cada fila por `pedido.id`, una clave sustituta:
salta, crece sin sentido para el usuario y expone la PK. En sala el pedido se
nombra por su número del turno — *"el 12"* — y ese concepto no existe en el
modelo.

## 2. Decisión

Un **correlativo con reinicio manual**, controlado por el cajero que abre el
turno. Sin reglas horarias: ni corte de jornada, ni ventanas de turno, ni zona
horaria, ni cruce de medianoche.

| Elemento | Qué es |
|---|---|
| `turno_caja` | una fila por cada apertura de turno; contiene el contador |
| `pedido.numero_turno` | correlativo dentro del turno, empieza en 1 |

Abrir un turno cierra el anterior e inserta una fila nueva con el contador en
cero. Eso, y solo eso, reinicia la numeración.

### 2.1 Por qué el reinicio inserta una fila

La alternativa —una tabla de parámetros con un contador que el cajero pone en
cero— se descartó por dos defectos:

1. **La unicidad deja de ser verificable.** Sin una fila que represente el
   período no hay clave contra la cual imponer `UNIQUE`.
2. **El período no queda registrado.** Después del reinicio no hay forma de
   reconstruir qué pedidos pertenecían al turno anterior.

Con la fila por apertura, `(id_turno_caja, numero_turno)` es único y lo impone
la base.

### 2.2 Los turnos del local

**Ejecutivo** y **a la carta** se registran como el `nombre` del turno. No
necesitan horarios configurados: el cajero abre el turno que corresponde y lo
nombra. Si más adelante conviene restringirlo a una lista, es un `CHECK`, no un
rediseño.

### 2.3 Contrapartida aceptada

El correlativo depende de una acción humana:

- Si el cajero olvida abrir turno nuevo, la numeración sigue corriendo desde el
  anterior. No es un error: el número igual es único.
- Si abre un turno por equivocación, la numeración reinicia en 1 y no se
  deshace (RN-4). Se corrige abriendo otro, no editando el anterior.
- La grilla muestra siempre qué turno está abierto y desde cuándo, para que el
  olvido se note (§8.5).

## 3. Fuera de alcance

- Arqueo de caja, montos, cierre contable. `turno_caja` registra apertura y
  cierre, nada más.
- Numeración por sede o por caja simultánea. Se asume **una sola caja abierta**
  (RN-3).
- Numeración de `cuenta` o de comprobantes fiscales. Este número es operativo,
  no tributario.

## 4. Modelo de datos

### 4.1 Tabla `turno_caja`

```sql
CREATE TABLE turno_caja (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre         varchar(40) NOT NULL,
    id_cajero      bigint,
    abierto_en     timestamptz NOT NULL DEFAULT now(),
    cerrado_en     timestamptz,
    ultimo_numero  int         NOT NULL DEFAULT 0,
    CONSTRAINT fk_turno_caja_cajero FOREIGN KEY (id_cajero)
        REFERENCES usuario (id) ON DELETE RESTRICT,
    CONSTRAINT ck_turno_caja_nombre CHECK (btrim(nombre) <> ''),
    CONSTRAINT ck_turno_caja_cierre CHECK (cerrado_en IS NULL OR cerrado_en >= abierto_en)
);

-- El histórico de turnos se lista por fecha de apertura.
CREATE INDEX ix_turno_caja_abierto_en ON turno_caja (abierto_en);
```

`nombre` es texto libre, pero no vacío: `ck_turno_caja_nombre` sigue el patrón de
`ck_pedido_plato_anulacion`, que ya exige `btrim(motivo_anulacion) <> ''`. Un
turno sin nombre no se distingue de otro en el histórico.

`id_cajero` es anulable solo por compatibilidad con procesos de soporte; todo
turno abierto desde la aplicación lo lleva (RN-7).

### 4.2 Una sola caja abierta

```sql
CREATE UNIQUE INDEX uk_turno_caja_abierto
    ON turno_caja ((true)) WHERE cerrado_en IS NULL;
```

Índice único parcial sobre expresión constante: admite como máximo una fila con
`cerrado_en IS NULL`.

> ✅ **Verificado** contra PostgreSQL 17.11. El segundo `INSERT` con
> `cerrado_en NULL` es rechazado por `uk_turno_caja_abierto`.

### 4.3 Columnas en `pedido`

```sql
ALTER TABLE pedido
    ADD COLUMN id_turno_caja bigint,
    ADD COLUMN numero_turno  int;
```

`numero_turno` y no `numero`: `mesa.numero` ya existe y son cosas distintas.

Tras la limpieza (§9.2) ambas pasan a `NOT NULL`, más:

```sql
ALTER TABLE pedido
    ADD CONSTRAINT fk_pedido_turno_caja FOREIGN KEY (id_turno_caja)
        REFERENCES turno_caja (id) ON DELETE RESTRICT,
    ADD CONSTRAINT uk_pedido_numero_turno UNIQUE (id_turno_caja, numero_turno);
```

`uk_pedido_numero_turno` es un btree sobre `(id_turno_caja, numero_turno)`, que
es exactamente el filtro y el orden por defecto de la grilla. **No hace falta
índice adicional sobre `id_turno_caja`.**

### 4.4 Trigger de asignación

Consistente con el resto del esquema: las reglas de negocio viven en la base.

```sql
CREATE FUNCTION fn_pedido_numero_turno() RETURNS trigger
LANGUAGE plpgsql AS $fn$
DECLARE
    v_turno bigint;
BEGIN
    UPDATE turno_caja
       SET ultimo_numero = ultimo_numero + 1
     WHERE cerrado_en IS NULL
    RETURNING id, ultimo_numero INTO v_turno, NEW.numero_turno;

    IF v_turno IS NULL THEN
        RAISE EXCEPTION 'No hay un turno de caja abierto: abra uno antes de registrar pedidos.';
    END IF;

    NEW.id_turno_caja := v_turno;
    RETURN NEW;
END;
$fn$;

CREATE TRIGGER tg_pedido_numero_turno
    BEFORE INSERT ON pedido
    FOR EACH ROW EXECUTE FUNCTION fn_pedido_numero_turno();
```

**`RAISE EXCEPTION` sin `ERRCODE`**, igual que `fn_cuenta_inmutable` y
`fn_detalle_inmutable`. Eso da `P0001`, que es el único código de trigger que
[`ApiControllerBase.TryTranslateDbError`](../Atipico.Api/Controllers/ApiControllerBase.cs)
traduce: devuelve el `MessageText` tal cual como `409`. Por eso el mensaje va
redactado en español para el usuario final. Un `ERRCODE` propio caería en el
`_ => null` del `switch` y saldría un 500 con stack trace.

### 4.5 Concurrencia — trade-off aceptado

El `UPDATE` toma un lock sobre la fila del turno abierto hasta el commit de la
transacción externa.

- **Sin huecos.** Un rollback deshace también el incremento.
- **Inserts serializados.** Dos pedidos simultáneos se ordenan entre sí.

Una `SEQUENCE` evitaría el lock pero dejaría huecos ante rollback y no se puede
reiniciar a criterio del cajero sin `ALTER SEQUENCE`.

#### La transacción del pedido debe ser corta

Medido: una transacción que inserta un pedido y tarda 3 s en commitear bloquea
2.3 s al insert de otra conexión. **El costo lo pone la duración, no el
volumen** — todos los inserts se encolan detrás del mismo lock de fila.

Consecuencia para `PedidosController`: la transacción que crea el pedido no
puede contener impresión de comanda, subida de comprobantes ni ninguna espera
de E/S. Abrir, insertar, commit.

Con transacciones cortas la serialización es irrelevante: 200 inserts desde 8
conexiones simultáneas se resolvieron sin duplicados ni huecos.

### 4.6 Apertura y cierre

Ambas en **una sola transacción**:

```sql
BEGIN;
UPDATE turno_caja SET cerrado_en = now() WHERE cerrado_en IS NULL;
INSERT INTO turno_caja (nombre, id_cajero) VALUES ('A la carta', :id_cajero);
COMMIT;
```

La numeración reinicia sola porque la fila nueva tiene `ultimo_numero = 0`.

### 4.7 No se cierra un turno con pedidos vivos

```sql
CREATE FUNCTION fn_turno_cierre() RETURNS trigger
LANGUAGE plpgsql AS $fn$
DECLARE
    v_vivos int;
BEGIN
    IF NEW.cerrado_en IS NULL OR OLD.cerrado_en IS NOT NULL THEN
        RETURN NEW;
    END IF;

    SELECT count(*) INTO v_vivos
    FROM pedido
    WHERE id_turno_caja = OLD.id
      AND estado NOT IN ('CERRADO','ANULADO');

    -- El mensaje sale tal cual en la pantalla del cajero, así que concuerda
    -- en número.
    IF v_vivos = 1 THEN
        RAISE EXCEPTION 'No se puede cerrar el turno: queda 1 pedido sin cerrar.';
    ELSIF v_vivos > 1 THEN
        RAISE EXCEPTION 'No se puede cerrar el turno: quedan % pedidos sin cerrar.', v_vivos;
    END IF;

    RETURN NEW;
END;
$fn$;

CREATE TRIGGER tg_turno_cierre
    BEFORE UPDATE ON turno_caja
    FOR EACH ROW EXECUTE FUNCTION fn_turno_cierre();
```

### 4.7.1 …ni con cuentas sin cobrar

Cerrar el pedido y cobrarlo son cosas distintas, y la diferencia es plata.
`PedidosController` impide pasar un pedido a `Cerrado` si le quedan platos sin
**facturar** (`v_pedido_plato_sin_cobrar`), pero facturar no es cobrar: un
pedido `CERRADO` con su cuenta `ABIERTA` es comida servida que nadie pagó.

Por eso el cierre de turno chequea también las cuentas, siguiendo el camino
`pedido` → `pedido_plato` → `detalle_cuenta` → `cuenta`:

```sql
SELECT count(DISTINCT c.id) INTO v_cuentas
FROM pedido p
JOIN pedido_plato   pp ON pp.id_pedido       = p.id
JOIN detalle_cuenta dc ON dc.id_pedido_plato = pp.id
JOIN cuenta         c  ON c.id               = dc.id_cuenta
WHERE p.id_turno_caja = OLD.id
  AND c.estado = 'ABIERTA';
```

- **`DISTINCT`** porque una cuenta reúne varias líneas de detalle, y un mismo
  pedido puede tener varias cuentas: cada comensal paga la suya.
- **`ANULADA` no bloquea.** Es un cierre deliberado y documentado
  (`ck_cuenta_anulacion` exige responsable y motivo). Si bloqueara, una sola
  anulación dejaría el turno imposible de cerrar para siempre.

**Los dos mensajes son distintos a propósito**, porque la acción del cajero
también lo es: *"sin cerrar"* lo manda a cerrar pedidos, *"sin cobrar"* lo manda
a cobrar. Y se chequea primero pedidos: uno sin cerrar casi siempre arrastra su
cuenta abierta, así que avisar por la cuenta primero lo mandaría a cobrarle a
una mesa que todavía está comiendo.

Vive en `sql/011_turno_cierre_cuentas.sql`, que reemplaza la función con
`CREATE OR REPLACE`. El trigger de `010` no se toca.

**Estados que bloquean:** en `pedido`, `ABIERTO`, `EN_PREPARACION` y
**`SERVIDO`** (terminales: `CERRADO` y `ANULADO`). En `cuenta`, solo `ABIERTA`.

`SERVIDO` es el caso que justifica la validación: el plato salió, la mesa está
comiendo, nadie pagó. Si no bloqueara, cerrar turno haría desaparecer de la
grilla exactamente las mesas en curso.

**El trigger es la red, no la interfaz.** `TurnosController` consulta primero y
devuelve *qué* pedidos bloquean, con su número y su comensal, para que el cajero
sepa a qué mesa ir (§7.2). El trigger cubre el caso de carrera y cualquier vía
que no pase por el controlador.

Se apila sobre una validación que ya existe: `PedidosController.Update` impide
pasar un pedido a `Cerrado` si quedan platos sin cobrar
(`v_pedido_plato_sin_cobrar`). Son dos niveles del mismo principio, en orden:
primero se cierra cada pedido, después el turno.

### 4.8 Traducción del error

En `ApiControllerBase.DescribirRestriccion`, junto a las demás:

```csharp
"uk_pedido_numero_turno" => "Ese número ya existe en el turno.",
```

Los dos triggers no necesitan entrada: `P0001` ya pasa su mensaje tal cual.

> ✅ **Verificado**: los dos triggers levantan `SQLSTATE = P0001`.

### 4.9 Permisos sobre la tabla nueva

`ALTER DEFAULT PRIVILEGES` (script_inicial.sql, BLOQUE 2) solo alcanza a los
objetos creados **por el mismo rol que lo ejecutó**. Si la migración la corre
otro usuario, `turno_caja` nace sin permisos y la aplicación falla en runtime con
*"permission denied for table turno_caja"* — un error que no aparece al migrar
sino al abrir el primer turno.

Por eso la migración otorga explícito, y revoca lo que el resto del esquema
también revoca:

```sql
GRANT SELECT, INSERT, UPDATE ON turno_caja TO app_restaurante;
REVOKE DELETE, TRUNCATE ON turno_caja FROM app_restaurante;
```

> ✅ **Verificado**: `app_restaurante` queda con `SELECT`, `INSERT` y `UPDATE`
> sobre `turno_caja`, y nada más.

### 4.10 El turno acota también quién y dónde

> Agregado después de las primeras pruebas de uso. `sql/012_pedido_unicidad_por_turno.sql`.

Dos reglas hermanas, con la misma forma: dentro de un turno, ni el nombre del
comensal ni la mesa pueden estar en dos pedidos **vivos** a la vez.

**«Vivos» y no «en todo el turno»**, y esto es la decisión, no un detalle. Una
mesa se ocupa y se libera tres o cuatro veces por turno, y `pedido_mesa` no se
puede borrar —`app_restaurante` no tiene `GRANT DELETE`—: con la regla estricta,
la primera mesa usada quedaría bloqueada hasta el cierre de caja y sin ninguna
forma de soltarla. Lo mismo con un nombre que se repite a lo largo del turno.

#### Comensal

`003_pedido_comensal_unico.sql` lo impuso globalmente; ahora el índice lleva el
turno adelante, **conservando el nombre de la restricción** (es el que llega en
`PostgresException.ConstraintName` y el que `DescribirRestriccion` traduce):

```sql
CREATE UNIQUE INDEX uk_pedido_comensal_activo
    ON pedido (id_turno_caja, comensal)
    WHERE estado IN ('ABIERTO', 'EN_PREPARACION');
```

Conviene saber que **hoy el alcance nuevo y el viejo coinciden**, antes de creer
que este cambio hace algo que no hace: un pedido `ABIERTO` o `EN_PREPARACION`
impide cerrar su turno (§4.7), así que todos los pedidos activos del sistema
están siempre en el único turno abierto. La diferencia aparece el día que esa
regla se relaje —o que alguien cierre un turno por SQL a mano, como pasa en
desarrollo—: ahí el índice global empezaría a rechazar nombres de turnos ya
terminados, y este no. El cambio hace **estructural** una propiedad que hoy es
una consecuencia.

Los `NULL` siguen exentos: Postgres no considera iguales dos `NULL` en un índice
único, así que varios pedidos activos sin comensal siguen siendo válidos.

#### Mesa

Antes no existía. `uk_pedido_mesa` solo impide asociar la misma mesa **dos veces
al mismo** pedido; dos pedidos distintos podían sentarse en la mesa 5 a la vez y
la interfaz se limitaba a avisar *"ya ocupada por otro pedido"* dejando pasar
igual.

No puede ser un índice único parcial: el predicado necesita `pedido.estado` y
`pedido.id_turno_caja`, y un índice sobre `pedido_mesa` solo puede mirar columnas
de `pedido_mesa`. Va como trigger, `fn_pedido_mesa_ocupada`, en `BEFORE INSERT OR
UPDATE` — el `UPDATE` también, porque `pedido_mesa` se expone por el CRUD
genérico y un `PUT` puede mover la fila a otra mesa sin pasar por el `INSERT`.

**`SERVIDO` cuenta como ocupada, y acá la mesa se separa del comensal.** Servido
el plato, el nombre ya cumplió su función y se puede reusar; la mesa sigue con
gente comiendo hasta que el pedido cierra. Terminales son solo `CERRADO` y
`ANULADO`, igual que en §4.7.

El trigger toma un `SELECT ... FOR UPDATE` sobre la fila de la mesa. No es
prudencia teórica: sin eso, dos meseros sentando gente en la mesa 5 al mismo
tiempo pasan los dos el conteo —ninguna transacción ve la fila que la otra
todavía no commiteó— y la doble ocupación entra igual. Es el mismo recurso que
§4.5, y con la misma condición: la transacción que asocia la mesa tiene que ser
corta.

> ✅ **Verificado** contra PostgreSQL 17 con el esquema completo: comensal
> repetido entre activos del mismo turno rechazado; varios `NULL` permitidos;
> misma mesa en dos pedidos vivos rechazada, también con el primero en `SERVIDO`;
> mesa y nombre reusables una vez cerrado el pedido, **dentro del mismo turno**;
> el `UPDATE` que mueve la fila a una mesa tomada, rechazado; y ambos liberados al
> rotar de turno. El trigger levanta `SQLSTATE = P0001`.

## 5. Reglas de negocio

| # | Regla |
|---|---|
| RN-1 | Todo pedido recibe `numero_turno` e `id_turno_caja` en el INSERT. |
| RN-2 | `numero_turno` empieza en 1 en cada turno. |
| RN-3 | Existe como máximo un turno abierto (`uk_turno_caja_abierto`). |
| RN-4 | El par `(id_turno_caja, numero_turno)` es único y el número es inmutable: no se reasigna ni se reutiliza. |
| RN-5 | Un pedido `ANULADO` **conserva** su número. El hueco es evidencia de auditoría. |
| RN-6 | Sin turno abierto no se pueden crear pedidos. |
| RN-7 | Un turno abierto desde la aplicación registra siempre el cajero. |
| RN-8 | Ni la aplicación ni la API envían `numero_turno` ni `id_turno_caja`: son de asignación exclusiva de la base. |
| RN-9 | Un turno cerrado no se reabre. Reiniciar la numeración es siempre abrir uno nuevo. |
| RN-10 | No se cierra un turno con pedidos en estado no terminal (§4.7). |
| RN-11 | La grilla muestra los pedidos del turno **cualquiera sea su estado**: pagados, sin pagar, servidos o anulados. |
| RN-12 | Un `Mesero` no puede salir del turno abierto en la grilla (§8.0). |
| RN-13 | Dentro de un turno, dos pedidos **activos** no comparten comensal. En otro turno el mismo nombre vuelve a estar libre (§4.10). |
| RN-14 | Dentro de un turno, una mesa no está en dos pedidos **vivos** a la vez. `SERVIDO` la mantiene ocupada; `CERRADO` y `ANULADO` la liberan (§4.10). |
| RN-15 | En pantalla el pedido se nombra por su `numero_turno` y su comensal, nunca por su `id` (§8.7). |

## 6. Domain

### 6.1 Entidad nueva

`Atipico.Domain/Entities/TurnoCaja.cs`:

```csharp
using Atipico.Domain.Interfaces;

namespace Atipico.Domain.Entities
{
    /// <summary>
    /// Una fila por apertura de caja. Contiene el correlativo de pedidos del
    /// turno. Ver docs/numero-pedido.md.
    /// </summary>
    public class TurnoCaja : IEntity
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = null!;

        public DateTimeOffset AbiertoEn { get; set; }
        public DateTimeOffset? CerradoEn { get; set; }

        /// <summary>Lo administra tg_pedido_numero_turno; no se escribe desde la aplicación.</summary>
        public int UltimoNumero { get; set; }

        public long? IdCajero { get; set; }
        public Usuario? Cajero { get; set; }

        public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    }
}
```

### 6.2 Cambios en `Pedido`

```csharp
/// <summary>Correlativo dentro del turno. Lo asigna la base. Ver docs/numero-pedido.md.</summary>
public int NumeroTurno { get; set; }

public long IdTurnoCaja { get; set; }
public TurnoCaja TurnoCaja { get; set; } = null!;
```

### 6.3 Configuración EF

`Atipico.Infraestructure/Persistence/Configurations/TurnoCajaConfiguration.cs`,
siguiendo el patrón de `PedidoConfiguration`:

```csharp
builder.ToTable("turno_caja");
builder.HasKey(t => t.Id);
builder.Property(t => t.Id).HasColumnName("id").UseIdentityAlwaysColumn();
builder.Property(t => t.Nombre).HasColumnName("nombre").HasMaxLength(40).IsRequired();
builder.Property(t => t.AbiertoEn).HasColumnName("abierto_en")
       .HasDefaultValueSql("now()").ValueGeneratedOnAdd();
builder.Property(t => t.CerradoEn).HasColumnName("cerrado_en");

// La escribe tg_pedido_numero_turno por fuera de EF: hay que releerla, nunca enviarla.
// Mismo patrón que ActualizadoEn con fn_touch.
builder.Property(t => t.UltimoNumero).HasColumnName("ultimo_numero")
       .ValueGeneratedOnAddOrUpdate();

builder.Property(t => t.IdCajero).HasColumnName("id_cajero");
builder.HasOne(t => t.Cajero).WithMany().HasForeignKey(t => t.IdCajero);
```

En `PedidoConfiguration`:

```csharp
builder.Property(p => p.NumeroTurno).HasColumnName("numero_turno").ValueGeneratedOnAdd();
builder.Property(p => p.IdTurnoCaja).HasColumnName("id_turno_caja").ValueGeneratedOnAdd();
builder.HasOne(p => p.TurnoCaja).WithMany(t => t.Pedidos)
       .HasForeignKey(p => p.IdTurnoCaja).OnDelete(DeleteBehavior.Restrict);
builder.HasIndex(p => new { p.IdTurnoCaja, p.NumeroTurno })
       .IsUnique().HasDatabaseName("uk_pedido_numero_turno");
```

Y el `DbSet<TurnoCaja> TurnosCaja` en `AppDbContext`.

`ValueGeneratedOnAdd()` hace que Npgsql agregue `RETURNING` al INSERT y traiga
los valores del trigger de vuelta a la entidad. **Sin verificar** — ver §10.1.

## 7. Application / API

### 7.1 Contrato de la grilla

El turno va **una vez en el sobre**, no repetido en cada fila:

```csharp
public sealed record GrillaPedidosDto(
    TurnoDto?                     Turno,     // null si no hay turno abierto
    IReadOnlyList<PedidoFilaDto>  Pedidos);

public sealed record TurnoDto(
    long Id, string Nombre, DateTimeOffset AbiertoEn,
    string? CajeroNombre, int TotalPedidos);
```

`Turno == null` distingue los dos estados vacíos de §8.4 sin que el cliente lo
infiera de una lista vacía.

### 7.2 `TurnosController`

**No extiende `EntityControllerBase<TurnoCaja>`:** abrir y cerrar no son CRUD, y
un `DELETE` sobre un turno no tiene sentido. Es un controlador propio, como
`AuthController`.

```
GET  api/turnos/abierto                    → TurnoDto | 204
GET  api/turnos                            → histórico (Admin, Cajero)
POST api/turnos       { nombre, idCajero } → cierra el vigente y abre uno
POST api/turnos/cerrar
```

- `POST api/turnos` es **una sola llamada**, no cerrar-y-después-abrir desde el
  cliente: partirlo deja una ventana sin caja abierta en la que los pedidos se
  rechazan.
- Antes de cerrar, consulta los pedidos vivos del turno y, si hay, responde
  `409` con `{ message, pedidos: [{ numeroTurno, comensal }] }`. El mensaje
  genérico del trigger es la red de atrás, no la respuesta esperada.
- Roles: abrir y cerrar quedan en `Admin` y `Cajero` (D-4).
- Debe envolver `AddAsync`/`UpdateAsync` en el `try/catch (DbUpdateException)`
  con `TryTranslateDbError`, como todo controlador que no usa la implementación
  base.

### 7.3 Acotar los pedidos al turno

`IEntityApiClient<T>` es genérico y no soporta query params. Hace falta una ruta
dedicada:

```
GET api/pedidos/turno/{idTurno}
GET api/pedidos/turno-abierto
```

Del lado de `Atipico.Web` eso implica un `HttpClient` crudo, que **debe
envolverse a mano en `EstadoOperaciones.SeguirAsync`** para no perder la barra
de progreso — igual que el `metodoPago` de `Pedidos/Edit.razor`.

> Esto además arregla un problema que ya existe: `Pedidos/Index.razor` hace
> `GetAllAsync()` y filtra en memoria, o sea que hoy carga **todos los pedidos
> que existieron**. Con unos cientos no se nota; con un año de operación sí.

## 8. Grilla

### 8.0 Alcance — el turno abierto por defecto, ampliable según rol

La grilla ya está partida por rol: los filtros de Id, Mesero y Creado están bajo
`AuthorizeView Roles="Mesero"` → `NotAuthorized`. Esa división coincide con
quién necesita qué, así que se aprovecha en lugar de romperla.

| Rol | Alcance |
|---|---|
| **Mesero** | **solo el turno abierto**, sin forma de salir. No pierde nada: nunca tuvo filtro de fecha |
| Admin, Cajero, Cocinero | turno abierto por defecto, con un filtro `Turno` más en el panel avanzado |

Para el mesero —el que dice "el 12" en el salón— el número es **siempre
inequívoco**. Para quien puede ampliar, la ambigüedad es manejable y aparece la
regla de §8.3.

Es el patrón `AuthorizeView`/`NotAuthorized` que ya envuelve a los otros tres
filtros, aplicado a uno más. No se quita nada de lo que hay hoy.

**Se muestran todos los pedidos del turno, cualquiera sea su estado** (RN-11).
El estado no restringe el alcance; los chips de estado siguen siendo un filtro
del usuario dentro de él.

### 8.1 Columna

| Propiedad | Valor |
|---|---|
| Encabezado | `N°` |
| Posición | primera |
| Alineación | derecha, cifras tabulares |
| Formato | `numero_turno` con padding a 3 dígitos: `001` … `999` |
| Desbordamiento | a partir de 1000, 4 dígitos sin romper el layout |
| Sin prefijo | `012`, no `P-012` ni `#012` |

**`EntityTable` necesita `TituloHeader="Comensal"`.** En móvil el título de la
tarjeta es la primera columna cuyo header no sea `Id`; sin esto, cada tarjeta
pasaría a titularse `012` y la lista se volvería ilegible.

### 8.2 Orden — vive en la página

`EntityTable` **no ordena nada**: las columnas devuelven texto ya formateado y
ordenar sobre el string da resultados plausibles pero erróneos. El orden lo
implementa `Pedidos/Index.razor`, como ya hace con `Creado`, `Estado`, `Tipo` y
`Comensal`.

- `N°` se ordena por `NumeroTurno` **como entero**, nunca por el texto: `010`
  debe quedar después de `009`.
- Se suma a la cadena de desempates existente, sin alterarla.
- Con el alcance en un solo turno, `N°` descendente equivale a "el más reciente
  arriba".

### 8.3 Ambigüedad — solo para quien puede ampliar

Si el filtro `Turno` abarca más de un turno, la columna debe mostrarlo
(`A la carta · 012`) o una columna `Turno` visible al lado. Sin eso aparecen dos
filas `012`.

Como el mesero no puede ampliar (RN-12), en la vista de sala esto nunca ocurre.

### 8.4 Estado vacío — dos casos

| Situación | Qué muestra |
|---|---|
| Turno abierto, sin pedidos | grilla vacía, encabezado normal. *"Sin pedidos en este turno."* |
| **No hay turno abierto** | **no se muestra la grilla**, sino la acción de abrir turno. *"No hay turno de caja abierto. Abrí uno para empezar a registrar pedidos."* |

El segundo no es un error: es el estado normal antes de empezar la operación, y
en el que queda el sistema después de la migración (§9). Es también el estado en
que un pedido sería rechazado (RN-6), así que la grilla y la API cuentan lo
mismo.

### 8.5 Encabezado de turno

```
Turno: A la carta · abierto 19:04 · Cajero: M. Ríos · 87 pedidos
```

Con el alcance de §8.0, este encabezado es **el único contexto que tiene el
usuario**: las filas ya no llevan fecha ni turno. Es además la única defensa
contra el olvido de §2.3, así que **se resalta cuando la apertura no es del día
en curso**.

La hora se formatea con `ToBoliviaTime()`, nunca con `ToLocalTime()`.

### 8.6 Páginas nuevas

- `Atipico.Web/Components/Pages/Turnos/Index.razor` — histórico, Admin/Cajero.
- La apertura y el cierre viven en el encabezado de la grilla de pedidos, no en
  una pantalla aparte: es donde el cajero ya está mirando.
- Registrar la ruta en `ApiRoutes.cs` y el enlace en `Navegacion.cs` con su
  `RolesCsv`, nunca editando `NavMenu.razor` ni `BarraInferior.razor`.

### 8.7 El pedido se nombra por su número, no por su id

> Agregado después de las primeras pruebas de uso.

`Pedidos/Edit.razor` encabezaba *"Editar pedido #12"*: la clave de la tabla, que
para el mesero no significa nada. Pasa a **`Pedido 012 · Juan`** — el correlativo
del turno y quién lo pidió, que es como se nombra el pedido en el salón. Lo mismo
en el `<PageTitle>` y en la confirmación de anular.

**La URL sigue llevando el `id`**, y tiene que seguir llevándolo: es único entre
turnos, y el correlativo no lo es. `/pedidos/12` es una dirección, no una
etiqueta; lo que se corrige es lo que se muestra.

Alcanza también al desplegable de `PedidoMesas/Edit.razor`, que listaba
`#12 - Juan`.

Antes de que el encabezado termine de cargar dice solo *"Pedido"*: mostrar `000`
sería inventar un número que no existe.

### 8.8 El filtro busca por `N°`, y lo ve todo el mundo

El panel de filtros tenía un filtro `Id`: con §8.1 y §8.7, buscaba sobre algo que
ya no se muestra en ninguna pantalla. Pasa a buscar por `N°`.

**Dos consecuencias que no son de maquetado.**

**Deja de estar escondido para el `Mesero`.** Los filtros bajo
`AuthorizeView Roles="Mesero"` → `NotAuthorized` (Id, Mesero, Creado, Turno) están
ahí porque amplían el alcance o son asunto de administración (§8.0). `N°` no es
ninguna de las dos: no saca al mesero del turno abierto, lo mueve **dentro** de
él. Y es exactamente su búsqueda — el que canta *"el 12"* en el salón es él.
Esconderle el filtro del número mientras se le deja el de nombre estaba al revés.

**Coincidencia exacta, no `Contains`.** El `Id` filtraba por subcadena, que sobre
una clave larga tiene sentido. Sobre un correlativo de una a tres cifras produce
sobre todo ruido: tipeando `1` aparecerían el 1, el 10 al 19, el 100 al 199 y el
21. Y la búsqueda no es exploratoria — el usuario ya sabe qué número quiere.

Los ceros de relleno no cuentan: `12`, `012` y `0012` son la misma búsqueda,
porque en pantalla el número se lee `012` y a mano se dice *"el 12"*. Lo que no
sea un número no devuelve nada, en vez de devolver todo.

En móvil el campo va con `inputmode="numeric"` para que el teléfono abra el
teclado numérico.

#### Alternativa registrada, no implementada — un solo buscador

> Anotada a pedido del usuario. **Se implementó lo de arriba tal cual**; esto queda
> para decidir más adelante.

Unificar los dos campos en la caja siempre visible: **«Buscar comensal o N°»**, y
que decida sola según lo que se tipee. El atractivo es concreto: la búsqueda más
frecuente del mesero —por número— dejaría de exigir abrir «Filtros», que es
justamente el gesto que el panel plegado le cobra hoy.

**El problema no es cómo enrutar, es que `comensal` es texto libre.** La regla
obvia —«si parsea como entero busco por número, si no por comensal»— rompe con un
comensal llamado `12`, y no es un caso rebuscado: la gente escribe el número de
mesa en ese campo todo el tiempo. Con esa regla, ese pedido se volvería
inencontrable por nombre.

Las salidas posibles, con lo que cuesta cada una:

| Salida | Qué pasa |
|---|---|
| Buscar en **los dos** y unir los resultados | nunca esconde nada, pero tipear `12` trae el pedido 012 **y** todos los comensales que contengan un 12 |
| Enrutar por parseo | limpio de leer, deja inencontrable al comensal `12` |
| Un prefijo explícito (`#12` = número) | inequívoco, pero hay que enseñárselo a alguien que está apurado en el salón |

La primera es la más honesta y probablemente la correcta: en una lista de un
turno, unos pocos falsos positivos se descartan de un vistazo, y esconder un
resultado no.

**Lo que cuesta además:** desaparece el filtro `N°` del panel, y con él el
contador de «Filtros (n)» deja de reflejar esa búsqueda — la caja siempre visible
no cuenta como filtro avanzado hoy, y ese detalle tendría que revisarse.

### 8.9 El panel también muestra el número

`Home.razor` lista los pedidos en curso, y era la última pantalla donde el pedido
no se nombraba por su número. La fila lo lleva adelante, con el mismo formato que
la columna `N°` de la grilla: tres dígitos y cifras tabulares, para que la
columna de números no baile entre filas.

**Se va el `Pedido #12` de reserva.** Cuando el pedido no tiene comensal, el
título caía en la clave de la tabla — justo lo que §8.7 sacó de las otras
pantallas. Con el número al frente, ese hueco ya no necesita rellenarse con nada:
el título pasa a decir *"Sin comensal"* y el número identifica igual.

#### De paso, el panel deja de traer todos los pedidos

Pasa a cargar por `GET api/pedidos/turno-abierto` en vez de `GetAllAsync()`.

No es un cambio de rendimiento colado de contrabando: es lo que hace que el
número mostrado **sea inequívoco por construcción**. Un `012` solo significa algo
junto a su turno, y el panel no tiene encabezado de turno donde decirlo (§8.5), así
que la única forma de que no mienta es que la lista no pueda contener dos turnos.

Hoy tampoco podría —un pedido activo impide cerrar su turno (§4.7), así que todos
los activos están en el turno abierto— pero eso es una consecuencia, no una
garantía, y el panel ya no depende de ella.

El efecto lateral es el que CLAUDE.md pide no perder de vista: el panel venía
trayendo **todos los pedidos que existieron** para mostrar cinco.

Sin turno abierto la lista queda vacía y aparece *"Ningún pedido en curso ahora
mismo."*, que es exactamente lo cierto.

## 9. Migración

> **El proyecto está en desarrollo.** No hay datos de producción que preservar.

### 9.1 Por qué se limpia en vez de hacer backfill

Inventar un turno sintético por día y repartir los pedidos históricos ahí sería
el único lugar del sistema donde se fabrica historia operativa, en un esquema
construido sobre lo contrario: auditoría, cuentas inmutables, anulaciones con
responsable y motivo.

Además, con `pedido` vacío las columnas entran `NOT NULL` de una vez.

### 9.2 Limpieza — fuera de la aplicación

`sql/dev_limpieza_transaccional.sql`, **sin numerar a propósito**: los scripts
numerados son la historia canónica de migración y este no debe formar parte de
ella ni ejecutarse jamás en un entorno real.

```sql
TRUNCATE
    comprobante_pago,
    detalle_cuenta,
    cuenta,
    pedido_plato,
    pedido_mesa,
    pedido
    RESTART IDENTITY CASCADE;
```

- **`comprobante_pago` va en la lista.** La migración 006 la enganchó a `cuenta`
  y las primeras versiones de este spec no la contemplaban.
- **Requiere superusuario.** El rol `app_restaurante` no tiene `GRANT DELETE` ni
  `TRUNCATE`: la aplicación no puede correr esto ni por accidente.
- `DELETE` no es alternativa. Verificado: un pedido **con hijos** choca contra
  `fk_pedido_mesa_pedido`, y `detalle_cuenta` **con filas** contra
  `fn_detalle_inmutable` (*"No se eliminan lineas de cuenta"*). Los triggers son
  `FOR EACH ROW`, así que sobre tablas vacías no se disparan — pero con datos
  reales ambas condiciones se cumplen siempre.
- Los catálogos quedan intactos: `usuario`, `plato`, `tipo_plato`, `mesa`. No
  hay que volver a sembrar ni recrear usuarios para entrar a la aplicación.
- **`comprobante_pago.storage_key` apunta a archivos en R2 que el script no
  borra.** En desarrollo quedan como huérfanos inofensivos; limpiar un entorno
  con volumen real exigiría barrer el bucket aparte.

> Este proyecto **no tiene tabla de auditoría**: `script_inicial.sql` deriva de
> `restaurante_db_sin_auditoria.sql`. Versiones anteriores de este spec asumían
> que sí, por haberse escrito contra otra copia del esquema.

### 9.3 `sql/010_turno_caja.sql` — escrito ✅

Script numerado a mano, en una transacción, con el patrón de
`009_pedido_direccion_entrega.sql`. En orden: `CREATE TABLE turno_caja` →
`uk_turno_caja_abierto` + `ix_turno_caja_abierto_en` → `ALTER TABLE pedido ADD
COLUMN` **ya `NOT NULL`** → FK + `uk_pedido_numero_turno` → las dos funciones y
sus triggers → los `GRANT`/`REVOKE` de §4.9.

**La salvaguarda es el propio `ADD COLUMN NOT NULL` sin `DEFAULT`:** si alguien
corre este script sin haber limpiado antes, falla acá mismo con *"column
id_turno_caja contains null values"* y la transacción entera se deshace.

> ✅ **Verificado** aplicando `script_inicial.sql` + las 8 migraciones en un
> contenedor limpio, con datos cargados: la corrida sin limpieza previa falla y
> **`turno_caja` no queda a medio crear**; con limpieza, ambos scripts pasan.

#### Runbook

1. `sql/dev_limpieza_transaccional.sql` — como superusuario.
2. `sql/010_turno_caja.sql`.
3. `sql/011_turno_cierre_cuentas.sql`.
4. `sql/012_pedido_unicidad_por_turno.sql` — comensal y mesa por turno (§4.10).
   A diferencia de los anteriores **se puede correr en caliente**, con turnos ya
   abiertos y pedidos cargados: reemplaza un índice y agrega un trigger, no toca
   datos.
5. **Abrir el primer turno.** La base queda sin turno abierto y el primer
   `INSERT` en `pedido` falla hasta que exista uno. Desde la aplicación es el
   botón «Abrir turno» de la grilla; desde psql, `sql/dev_abrir_turno.sql`:

```
psql <conexión> -v nombre='Ejecutivo' -f sql/dev_abrir_turno.sql
```

   Sin numerar, como la limpieza, pero a diferencia de ella **no necesita
   superusuario**: `app_restaurante` ya tiene `INSERT` y `UPDATE` sobre
   `turno_caja`. Admite `-v cajero=<nombre_usuario>` y `-v cerrar_vigente=1`
   para rotar de turno. Los dos caminos de fallo —cajero inexistente, turno ya
   abierto— cortan con código 3 sin tocar ninguna fila.

5. Regenerar `sql/schema_completo.sql` (pendiente, ver §12.2).

#### La migración no rompe la aplicación

Con un turno abierto, **la aplicación sigue funcionando sin tocar una línea de
C#**: el trigger llena `numero_turno` e `id_turno_caja` en el `INSERT`, y las
entidades actuales de EF ni mencionan esas columnas. Verificado: todos los
`INSERT` de prueba las omitieron, igual que hace EF hoy.

Eso permite aplicar la base (Fase 2) y seguir desarrollando el resto sin quedar
a mitad de camino.

## 10. Criterios de aceptación

> CA-1 a CA-20 **ya fueron ejecutados** contra PostgreSQL 17.11, con
> `script_inicial.sql` + las 8 migraciones aplicadas en orden en un contenedor
> limpio y los scripts de §9 tal como están escritos. Los ✅ pasaron. Siguen
> siendo criterios: hay que reproducirlos en la suite, no dejarlos como
> verificación manual de una vez.

- [x] ✅ CA-1 — Abrir turno e insertar un pedido asigna `numero_turno = 1`; el siguiente, `2`.
- [x] ✅ CA-2 — El primer pedido del turno siguiente vuelve a `1`.
- [x] ✅ CA-3 — Un segundo `INSERT` en `turno_caja` con `cerrado_en NULL` es rechazado.
- [x] ✅ CA-4 — Insertar sin turno abierto lanza la excepción del trigger, capturable por `SQLSTATE`.
- [x] ✅ CA-5 — 8 conexiones simultáneas × 25 pedidos → 200 números distintos, 1 a 200, sin huecos.
- [x] ✅ CA-6 — Un rollback no consume número: el siguiente pedido toma el que quedó libre.
- [x] ✅ CA-7 — Anular no modifica `numero_turno` ni libera el número.
- [x] ✅ CA-8 — Un `INSERT` con `numero_turno` explícito es ignorado (RN-8).
- [x] ✅ CA-9 — Un duplicado dentro del turno es rechazado por la constraint única.
- [x] ✅ CA-10 — Pedidos a las 23:58 y 00:03 del mismo turno reciben números consecutivos: no hay reinicio a la medianoche.
- [x] ✅ CA-11 — Un pedido con hijos no se borra; las líneas de cuenta con filas no se borran nunca.
- [x] ✅ CA-12 — `TRUNCATE ... RESTART IDENTITY CASCADE` vacía lo transaccional y deja los catálogos intactos.
- [x] ✅ CA-13 — Cerrar con pedidos `ABIERTO` es rechazado; con uno solo en `SERVIDO` también; con todos en `CERRADO` pasa y el siguiente turno reinicia en `1`.
- [x] ✅ CA-14 — Los dos triggers levantan `SQLSTATE = P0001`, que es el único que `TryTranslateDbError` traduce (§4.4).
- [x] ✅ CA-15 — El mensaje de cierre concuerda en número: *"queda 1 pedido"* / *"quedan 3 pedidos"*.
- [x] ✅ CA-16 — `app_restaurante` queda con `SELECT`, `INSERT`, `UPDATE` sobre `turno_caja` y nada más (§4.9).
- [x] ✅ CA-17 — `010_turno_caja.sql` corrido sin la limpieza previa falla y revierte entero: `turno_caja` no queda a medio crear (§9.3).
- [x] ✅ CA-18 — Un pedido `CERRADO` con su cuenta `ABIERTA` impide cerrar el turno: *"queda 1 cuenta sin cobrar"*. Con dos, *"quedan 2 cuentas"*.
- [x] ✅ CA-19 — Una cuenta `ANULADA` **no** bloquea el cierre.
- [x] ✅ CA-20 — Cobrada la cuenta, el turno cierra y el siguiente reinicia en `1`.
- [x] ✅ CA-21 — El error de la base llega como `409` con el mensaje en español, no como 500. Cubierto del lado API (`uk_turno_caja_abierto` → 409 traducido). El tramo Blazor lo hace `TurnoApiClient`, que lee el `message` igual que `EntityApiClient`; **sin ejercitar de punta a punta**.
- [x] ✅ CA-22 — El rechazo de cierre incluye la lista de pedidos que lo impiden, con número y comensal, ordenada por número.
- [x] ✅ CA-23 — `ValueGeneratedOnAdd` trae `numero_turno` e `id_turno_caja` del trigger a la entidad tras `SaveChangesAsync`. **Verificado contra PostgreSQL 17, y no bastaba** — ver §13.7.
- [x] ✅ CA-24 — La transacción de creación de pedido no contiene E/S externa: `Create` es `AddAsync` + `SaveChangesAsync` y nada más (§4.5).
- [x] ✅ CA-25 — Un `Mesero` no puede ver pedidos fuera del turno abierto. No es solo UI: `GET api/pedidos/turno/{id}` responde `Forbid` a quien no puede ampliar.
- [x] ✅ CA-26 — Un `Admin` puede ampliar el filtro de turno. **Reformulado**: el filtro elige un turno, no un rango, así que la ambigüedad de §8.3 no puede darse y la identidad del turno la lleva el encabezado.
- [x] ✅ CA-27 — La grilla muestra pedidos `CERRADO` y `ANULADO` del turno: la ruta no filtra por estado (RN-11).
- [x] ✅ CA-28 — Sin turno abierto la API devuelve el sobre con `Turno` en null y la grilla muestra la acción de abrir turno, no una tabla vacía (§8.4).
- [x] ✅ CA-29 — Ordenar por `N°` ordena por el entero, nunca por el texto.
- [x] ✅ CA-30 — En móvil el título de la tarjeta sigue siendo el comensal (`TituloHeader="Comensal"`).
- [x] ✅ CA-31 — El encabezado se resalta cuando el turno abierto no es del día en curso.

Agregados con `sql/012` y §8.7:

- [x] ✅ CA-32 — Dos pedidos activos del mismo turno no comparten comensal; el rechazo llega como 409 en español.
- [x] ✅ CA-33 — Varios pedidos activos **sin** comensal siguen siendo válidos (los `NULL` no colisionan).
- [x] ✅ CA-34 — Cerrado el pedido, el mismo nombre se puede volver a usar **dentro del mismo turno**.
- [x] ✅ CA-35 — Rotado el turno, el nombre y la mesa del turno anterior vuelven a estar libres.
- [x] ✅ CA-36 — Una mesa en dos pedidos vivos del mismo turno es rechazada, **también** con el primero en `SERVIDO`.
- [x] ✅ CA-37 — Cerrado el pedido, la mesa se reutiliza en el mismo turno.
- [x] ✅ CA-38 — Un `PUT` sobre `pedido_mesa` que mueva la fila a una mesa tomada es rechazado.
- [x] ✅ CA-39 — La mesa ocupada aparece en el desplegable pero **no se puede elegir**, y no es el valor por defecto.
- [ ] CA-40 — El encabezado muestra `Pedido 012 · Juan` en lugar del id, y *"Pedido"* mientras carga. **Sin ejercitar en el navegador.**
- [x] ✅ CA-41 — El filtro busca por `N°`: `12`, `012` y `0012` traen el mismo pedido, y solo ese.
- [x] ✅ CA-42 — Un filtro no numérico no devuelve nada, en vez de devolver todo.
- [x] ✅ CA-43 — Un `Mesero` **sí** ve el filtro de `N°` (§8.8), a diferencia de los de Id, Mesero, Creado y Turno.
- [x] ✅ CA-44 — El panel muestra el `N°` de cada pedido en curso, con el mismo formato que la grilla (§8.9).
- [x] ✅ CA-45 — Un pedido sin comensal ya no se titula `Pedido #12` en el panel: el número queda al frente y el título dice *"Sin comensal"*.
- [x] ✅ CA-46 — El panel carga por `turno-abierto`, así que su lista no puede mezclar dos turnos (§8.9).

CA-21 a CA-46 se verifican por prueba automática o por inspección del código, según
el caso; ninguno de los dos es lo mismo que haber abierto la pantalla. Lo que falta
es el paso por el navegador: abrir un turno, cargar pedidos, intentar cerrar con una
mesa comiendo y ver el mensaje.

### 10.1 Qué queda sin verificar

Lo probado es el comportamiento de la base. **Nada del lado de EF Core está
verificado**, en particular §6.3: si `ValueGeneratedOnAdd()` hace que Npgsql
agregue `RETURNING` y traiga los valores del trigger de vuelta a la entidad. Es
la afirmación de mayor riesgo que queda en pie (CA-23) y conviene comprobarla
antes de construir la capa de aplicación encima.

Tampoco está verificado el camino completo del error: que el `P0001` del trigger
llegue a la pantalla del cajero como un 409 legible (CA-21). Lo verificado es
solo que el código es el que `TryTranslateDbError` sabe traducir.

## 11. Decisiones abiertas

Ninguna bloquea. Queda una diferida, anotada para más adelante.

| # | Decisión | Bloquea |
|---|---|---|
| D-6 | ¿Un solo buscador para comensal y `N°`, en vez de dos campos? Ver §8.8 | nada — lo actual funciona |
| — | **Cerrada (c):** los criterios que dependen de triggers se prueban por contrato SQL↔modelo, sin Docker. Ver §12.1 | §12 |
| — | **Cerrada:** padding a 3 dígitos con desborde natural a 4 (`D3`). Ver §8.1 | §8.1 |
| — | **Cerrada:** abrir y cerrar turno son `Admin` y `Cajero`; el mesero no abre caja | §7.2 |
| — | **Cerrada:** cerrar turno exige cuentas cobradas (`sql/011`) | §4.7.1 |
| — | **Cerrada:** alcance de la grilla — turno abierto por defecto, ampliable salvo `Mesero` | §8.0 |
| — | **Cerrada:** la grilla es Blazor Server, `EntityTable` + `Pedidos/Index.razor` | §8 |
| — | **Cerrada:** nombre `numero_turno` / `NumeroTurno` | §4.3 |
| — | **Cerrada:** sin turno abierto se rechaza el pedido (RN-6) | §4.4 |

**D-3** se cerró sin ceremonia: `ToString("D3")` rellena hasta 3 dígitos y a partir
de 1000 escribe 4 solo, sin romper nada. No hace falta saber si un turno pasa los
999 — el formato aguanta las dos respuestas.

---

## 12. Plan de acción

### 12.1 D-2, resuelta por un camino que no estaba en la lista

Los cuatro proyectos de test usan **xUnit + Moq**, con `Moq.EntityFrameworkCore`
para mockear `DbSet`/`DbContext`. Ningún test toca una base real.

Pero toda la lógica de esta feature vive en triggers y constraints. **Un
`DbContext` mockeado no puede verificar** que el correlativo no se duplique bajo
concurrencia, que el cierre se bloquee, ni que el trigger devuelva sus valores.

El planteo original era binario: Testcontainers, o verificación manual. Hay un
tercer camino, y es el que el repo **ya practica**: `ModeloTipoPedidoTest` lee
`sql/008_pedido_tipo.sql` y compara sus literales contra el conversor de EF, sin
levantar nada. No verifica que el CHECK funcione —eso lo da por hecho— sino que
las dos fuentes de verdad sigan diciendo lo mismo.

Aplicado acá, `ModeloTurnoCajaTest` parte el problema en dos mitades honestas:

| | Cómo se verifica |
|---|---|
| **El contrato** entre `sql/010` y el modelo EF: columnas, nombres de restricción, qué escribe la aplicación y qué no | automático, sin Docker — es lo que se rompe al editar una de las dos fuentes y olvidar la otra |
| **El comportamiento** de la base: correlativo sin huecos bajo concurrencia, cierre bloqueado, `RETURNING` | a mano contra PostgreSQL 17, con el esquema real (CA-1 a CA-20) |

Es la división correcta porque coincide con **cómo se rompe cada cosa**. El
comportamiento del trigger no va a cambiar solo: está escrito, verificado y
congelado en un script numerado que nadie edita. El contrato entre el SQL y el
modelo, en cambio, se rompe cada vez que alguien toca uno de los dos lados — y ese
es justo el fallo que no se ve al compilar ni al arrancar.

**Que esto no es gratis:** si alguien reescribe `fn_pedido_numero_turno` en una
migración futura, ningún test lo va a atrapar. Testcontainers sigue siendo la
respuesta completa, y sigue siendo una decisión de equipo pendiente; esto es lo
que se puede tener hoy sin cambiarle las herramientas al repo.

Además, `TurnosControllerTests` cubre con Moq lo que sí vive en C#: quién puede
abrir y cerrar, de dónde sale el cajero, que el turno vigente no se cierre si la
apertura va a fallar, y que el rechazo por pedidos vivos traiga la lista y
concuerde en número.

### 12.2 Fases

Los 9 proyectos ya existen. No hay andamiaje que crear.

| Fase | Qué | Estado |
|---|---|---|
| ~~2~~ | `sql/dev_limpieza_transaccional.sql`, `sql/010_turno_caja.sql`, `sql/011_turno_cierre_cuentas.sql` | ✅ |
| ~~1~~ | `ModeloTurnoCajaTest` (8 pruebas) + `TurnosControllerTests` (22) | ✅ |
| ~~3~~ | `TurnoCaja`, columnas en `Pedido`, configuraciones EF, `DbSet` | ✅ |
| ~~4~~ | `TurnosController`, rutas de pedidos por turno, `DescribirRestriccion` | ✅ |
| ~~5~~ | `Pedidos/Index.razor`: columna, orden, alcance por rol, encabezado, estados vacíos | ✅ |
| ~~6~~ | `Turnos/Index.razor`, `Navegacion.cs` | ✅ |
| **7** | Regenerar `sql/schema_completo.sql` | **fuera de alcance, ver abajo** |
| ~~8~~ | `sql/012`: comensal y mesa únicos por turno (§4.10) + el pedido se nombra por su número (§8.7) | ✅ |
| ~~9~~ | El filtro de la grilla busca por `N°` y deja de estar escondido para el `Mesero` (§8.8) | ✅ |
| ~~10~~ | El panel muestra el `N°` y carga por `turno-abierto` (§8.9) | ✅ |

La suite completa queda en **158 pruebas, todas en verde**.

La fase 8 no estaba en el plan: salió de las primeras pruebas de uso, ya con la
feature andando. Es la parte que **sí** siguió el orden correcto en espíritu —el
usuario probó, encontró qué faltaba y lo pidió— aunque el código volvió a
escribirse antes de que el spec lo registrara.

#### La fase 7 no se hizo, y es correcto que no se haya hecho

`sql/schema_completo.sql` resultó no ser lo que el plan suponía. No es salida de
`pg_dump`: son 460 líneas curadas a mano, con el bloque de MANTENIMIENTO que
empareja cada CHECK con su enum de C#, el DRIFT CONOCIDO de `ck_cuenta_metodo`, y
BLOQUE 2 con la creación del rol `app_restaurante` —que `pg_dump` no emite—.

Y ya estaba desactualizado **antes** de esta feature: llega hasta
`005_plato_habilitado_hasta.sql` y no incluye 006, 007, 008 ni 009. Ponerlo al día
no es un paso de esta tarea, es regenerar seis migraciones de esquema y decidir si
el archivo sigue siendo un documento comentado o pasa a ser salida cruda.

Lo que sí se hizo es lo urgente: el archivo ahora abre con un aviso de
**DESACTUALIZADO** que dice exactamente qué falta y qué hacer en su lugar. Sin eso,
alguien lo usa para levantar un ambiente nuevo y se encuentra con que `pedido` no
tiene `id_turno_caja` — un error a la primera inserción, lejos del archivo que lo
causó.

#### Lo que cambió respecto de lo diseñado

Cinco desvíos, todos por algo que apareció al escribir el código:

1. **`GrillaPedidosDto` lleva `Pedido`, no un `PedidoFilaDto`.** Una fila necesita
   comensal, estado, tipo, dirección, coordenadas, mesero, creado e id: el DTO
   habría sido `Pedido` con otro nombre, y `EntityTable` exige `IEntity`. El sobre
   —que era el punto: el turno una vez y no repetido en cada fila— se conserva.
2. **El cajero no viaja en el cuerpo de `POST api/turnos`.** Sale del token, como
   en `ComprobantesController`. Con `idCajero` en el cuerpo, cualquiera abre un
   turno a nombre de otro. El request quedó en `{ nombre }`.
3. **`IEntityService<T>` ganó `FindAsync`.** §7.3 pedía acotar los pedidos al
   turno, y el servicio solo sabía `GetAllAsync`. `IRepository` ya tenía
   `FindAsync`; faltaba exponerlo. Es lo que hace que el recorte sea SQL y no un
   filtro en memoria sobre la tabla entera.
4. **`EntityTable` ganó `ShowCreate`.** Mirando un turno que no es el abierto,
   "+ Nuevo" mentiría: el pedido caería en el turno abierto, no en el que se está
   viendo.
5. **§8.3 quedó sin objeto.** El filtro elige **un** turno, no un rango, así que
   dos filas `012` no pueden convivir. La identidad del turno la lleva el
   encabezado. La regla de §8.3 sigue siendo cierta; simplemente no hay caso.

Y dos cosas menores que no estaban previstas: sin turno abierto la grilla
desaparece y con ella el panel de filtros, así que quien puede ver el histórico
tiene un selector propio en ese estado vacío; y "Limpiar" también vuelve al turno
abierto, porque el turno cuenta como filtro puesto en el contador del botón.

#### El orden se invirtió, y conviene saberlo

El plan ponía los tests primero. La base se escribió antes, a pedido, para poder
aplicarla sin bloquear el resto del desarrollo. La consecuencia honesta: los
tests de la Fase 1 se escribirán contra SQL que **ya funciona**, así que serán
tests de caracterización y no rojo-verde. No es un problema en sí —los 17
criterios ya se verificaron a mano contra el esquema real— pero significa que
no van a encontrar nada nuevo: sirven para que no se rompa después, no para
descubrir que está mal ahora.

Lo que sí sigue siendo rojo-verde es todo lo de las Fases 3 a 6, que no existe.

**D-1 se resolvió por la afirmativa**, y como `010` ya estaba entregado salió
como `sql/011_turno_cierre_cuentas.sql` con `CREATE OR REPLACE FUNCTION`. Es
correcto tanto si `010` ya se corrió como si no: en el peor caso son dos
archivos en vez de uno.

**Verificar CA-23 apenas termine la Fase 3**, antes de construir la Fase 4
encima: si `ValueGeneratedOnAdd` no trae los valores del trigger, cambia la
estrategia de mapeo y arrastra todo lo que venga después.

### 12.3 Riesgos

| Riesgo | Mitigación |
|---|---|
| `ValueGeneratedOnAdd` no trae los valores del trigger | verificar en Fase 3, antes de Fase 4 |
| La validación de cierre resulta rígida en operación | quitar el trigger de §4.7 y mostrar los pedidos vivos de turnos anteriores; no toca el esquema |
| La ruta cruda de §7.3 pierde la barra de progreso | `EstadoOperaciones.SeguirAsync` explícito, como el `metodoPago` de `Pedidos/Edit.razor` |
| `numero_turno` se cuela en un `PUT` desde la UI | RN-8 + `ValueGeneratedOnAdd`; CA-8 lo cubre |

---

## 13. Bitácora

### 13.1 Diseños descartados

| # | Diseño | Por qué cayó |
|---|---|---|
| 1 | Exponer `pedido.id` | clave sustituta: salta, no es referencia de negocio |
| 2 | Correlativo diario derivado de `creado_en` + columna `fecha_operativa` | la unicidad era imponible sin materializar la fecha, así que la columna no se justificaba |
| 3 | Correlativo diario derivado, sin columna | correcto mientras la regla de corte fuera fija; cayó al planificarse configurable |
| 4 | Tabla de parámetros con `hora_inicio`/`hora_fin` por turno | los turnos no cubren 24 h: quedaban pedidos sin número en los huecos. Y un corte configurable vuelve la función `STABLE`, lo que hace ilegal el índice sobre expresión |
| 5 | Tabla de una fila con contador que el cajero pone en cero | la unicidad deja de ser verificable y el período no queda registrado |
| **6** | **`turno_caja`: una fila por apertura, reinicio manual** | **adoptado** |

El salto de 4 a 6 lo disparó pedir reinicio manual. Eliminó de un golpe la zona
horaria, la hora de corte, el cruce de medianoche y la tabla de horarios.

### 13.2 Un error corregido

Una versión anterior afirmaba que una función con `AT TIME ZONE` debía ser
`STABLE` y no se podía indexar. Es falso: verificado, `timezone(text,
timestamptz)` tiene `provolatile = 'i'` y el índice sobre la expresión se crea.
Se registra porque ese error sostenía por sí solo una decisión de diseño.

### 13.3 Verificado empíricamente

PostgreSQL 17.11 en contenedor, sobre el esquema real completo: el índice único
parcial; el trigger de correlativo (asigna, ignora valores explícitos, sin
huecos ante rollback, 8 conexiones concurrentes); el lock serializando 2.3 s; el
bloqueo de `DELETE`; la validación de cierre con `ABIERTO` y `SERVIDO`.

### 13.4 Resincronización contra el repo

Este spec se escribió primero contra un sandbox con una copia del esquema, no
contra `Atipico`. Al traerlo, hubo que corregir:

- **`ERRCODE` propio → `P0001`.** `TryTranslateDbError` solo traduce `P0001`;
  un código propio habría dado un 500 con stack trace en vez de un 409 en
  español.
- **Migración EF Core → `sql/NNN_*.sql`** a mano.
- **`ApplicationDbContext` en Domain → `AppDbContext` en Infraestructure**, con
  `IEntityTypeConfiguration` por entidad.
- **`Domain.Models` → `Atipico.Domain.Entities`**, implementando `IEntity`.
- **El orden no vive en la grilla sino en la página**; las columnas devuelven
  texto formateado.
- **`TituloHeader`**: sin él, `N°` como primera columna se vuelve el título de
  cada tarjeta en móvil.
- **`TRUNCATE` requiere superusuario**: `app_restaurante` no tiene el grant.
- **Bolivia UTC-4** (`ToBoliviaTime()`), no `America/Lima`.
- **Los 9 proyectos ya existen**, con xUnit + Moq. Se descartó el plan de
  andamiaje completo que asumía crearlos.
- **El alcance de la grilla** chocaba con siete filtros existentes; se resolvió
  apoyándose en la división por rol que el archivo ya tenía (§8.0).

### 13.5 Lo que cambió al escribir los scripts

Escribir el SQL contra el esquema real corrigió cuatro cosas más que la lectura
del repo no había alcanzado:

- **`comprobante_pago` faltaba en la limpieza.** La migración 006 la enganchó a
  `cuenta`; sin ella el `TRUNCATE` habría fallado o dejado datos colgando.
- **No existe tabla de auditoría.** `script_inicial.sql` deriva de
  `restaurante_db_sin_auditoria.sql`. La decisión D-5 ("¿se limpia también
  `auditoria`?") se eliminó por no tener objeto — su número queda quemado, por eso
  la siguiente decisión abierta es D-6 y no D-5.
- **Los permisos no se heredan solos.** `ALTER DEFAULT PRIVILEGES` cubre lo que
  cree el mismo rol que lo ejecutó; si la migración la corre otro usuario, la
  tabla nace sin permisos y falla al abrir el primer turno, no al migrar. La
  migración otorga explícito (§4.9).
- **El mensaje de cierre no concordaba en número.** Decía *"quedan 1 pedidos"*, y
  ese texto va literal a la pantalla del cajero vía `P0001` → 409.

Se agregaron además `ck_turno_caja_nombre` (un turno sin nombre no se distingue
de otro en el histórico) e `ix_turno_caja_abierto_en` para el listado.

### 13.6 `011` — cerrar turno exige cuentas cobradas

D-1 se cerró por la afirmativa: no basta con que los pedidos estén `CERRADO`,
sus cuentas tienen que estar cobradas.

El motivo es que `PedidosController` ya impide cerrar un pedido con platos sin
**facturar**, y eso se lee fácil como "ya está cobrado". No lo está: facturar es
emitir la línea, cobrar es que entre la plata. Un pedido `CERRADO` con cuenta
`ABIERTA` es comida servida que nadie pagó, y dejar cerrar caja con eso adentro
es justamente lo que un cierre de turno tiene que impedir.

Salió como migración aparte porque `010` ya estaba entregado. Los scripts
numerados aplicados no se editan.

### 13.7 `ValueGeneratedOnAdd` no alcanzaba: el 201 mentía

CA-23 era la afirmación de mayor riesgo del spec: que EF trajera de vuelta los
valores que pone el trigger. Se verificó contra PostgreSQL 17 con el esquema real,
y **pasó**: `numero_turno` e `id_turno_caja` vuelven poblados después de
`SaveChangesAsync`.

Lo que no pasó fue CA-8. Un `INSERT` con `numero_turno` explícito seguía
guardándose bien en la base —el trigger lo pisa, como estaba verificado— pero la
entidad en memoria conservaba el valor inventado, y **esa** es la que se serializa
en el `201`. Un cliente que mandara `{"numeroTurno": 77}` recibía de vuelta un 77
que en la base era un 3.

La causa es una sutileza de EF: `ValueGeneratedOnAdd()` significa *"la base pone un
valor **si** la aplicación no puso ninguno"*. Con un valor presente, EF lo manda y
deja de pedir `RETURNING` para esa columna. La corrección es
`SetBeforeSaveBehavior(PropertySaveBehavior.Ignore)`, que hace que EF nunca lo
mande; `SetAfterSaveBehavior(Ignore)` cierra el otro lado (RN-4).

Vale anotar cómo apareció, porque es el argumento a favor de §12.1: no lo encontró
un test de la base ni uno del modelo, sino **ejercitar EF contra PostgreSQL de
verdad**. Ahora está congelado en `ModeloTurnoCajaTest`, que verifica los dos
`SaveBehavior` — una prueba que sin esta historia se leería como ceremonia.

### 13.8 Lo que se decidió al implementar

Cinco desvíos del diseño, detallados en §12.2: el sobre lleva `Pedido` en vez de un
`PedidoFilaDto` que habría sido `Pedido` con otro nombre; el cajero sale del token y
no del cuerpo; `IEntityService<T>` ganó el `FindAsync` que `IRepository` ya tenía;
`EntityTable` ganó `ShowCreate`; y §8.3 quedó sin objeto porque el filtro elige un
turno y no un rango.

D-2 se resolvió por un camino que no estaba en la lista —contrato SQL↔modelo con las
herramientas que el repo ya usa, comportamiento del trigger verificado a mano— y D-3
se resolvió sola: `D3` rellena a tres dígitos y desborda a cuatro sin romper nada, así
que no hace falta saber si un turno pasa los 999.

Y apareció algo ajeno a la feature: `sql/schema_completo.sql` estaba desactualizado
desde `005`. No se regeneró (ver §12.2), pero ahora avisa que lo está.

### 13.9 Lo que sacaron a la luz las primeras pruebas de uso

Tres cosas, y ninguna se habría visto sin usar la pantalla:

**El encabezado decía `#12`.** Con el correlativo ya en la grilla, la ficha del
pedido seguía titulándose por la clave de la tabla. Es el defecto que originó
toda la feature —§1: *"la grilla identifica cada fila por `pedido.id`"*— y sobrevivió
en la pantalla de al lado porque el spec solo miró la grilla.

**La unicidad del comensal era global.** Nadie la había atado al turno porque
hasta ahora no había turno al que atarla. El cambio es en buena medida
declarativo (§4.10 explica por qué hoy da lo mismo), y vale igual: deja escrito
en el esquema algo que hoy solo es cierto por carambola.

**La mesa no tenía ninguna validación.** Esta sí era un agujero real: dos pedidos
podían sentarse en la mesa 5 a la vez y la interfaz solo avisaba, con un texto al
costado de una opción que igual dejaba elegir. Ahora la rechaza la base y la
interfaz deja de ofrecerla.

Sobre el alcance de las dos reglas hubo que elegir entre *"por turno"* y *"entre
los pedidos vivos del turno"*, que suenan igual y no lo son. Decidió un detalle
del esquema: `pedido_mesa` no se puede borrar, así que con la lectura estricta la
primera mesa usada quedaba muerta hasta el cierre de caja.

**El orden se volvió a invertir.** El pedido llegó como una corrección sobre algo
ya probado, se implementó y recién después se escribió acá. El usuario lo marcó:
primero el spec, después el plan. Queda anotado como lo que es —registro de lo
hecho, no propuesta previa— y como el orden a respetar de acá en adelante.
