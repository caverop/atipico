# Número de mesa en la grilla de pedidos

Especificación funcional y técnica para SCRUM-16 (Jira, proyecto `atipico`): "Agregar numero
de mesa en grilla pedidos". Documento de referencia previo a la implementación: recoge las
decisiones tomadas y su porqué, para no volver a discutirlas al escribir el código.

- **Estado:** **propuesto**. Sin código todavía.
- **Origen:** [SCRUM-16](https://caverop.atlassian.net/browse/SCRUM-16), tipo Task, sin
  descripción ni criterios de aceptación en el ticket — el alcance de este documento es la
  interpretación de lo pedido, hecha al analizarlo.
- **Alcance:** que `/pedidos` (`Pedidos/Index.razor`) muestre a qué mesa(s) corresponde cada
  pedido, sin tener que entrar a editarlo.
- **Fuera de alcance:** cambiar cómo se asocia una mesa a un pedido (`Pedidos/Edit.razor`,
  `PedidoMesasController`) — eso ya existe y no se toca. Tampoco se agrega filtro por mesa;
  el ticket solo pide mostrarla.

---

## 1. Contexto: qué existe hoy y qué falta

`Pedido` no tiene una mesa: tiene cero, una o varias, a través de la tabla puente
`PedidoMesa` (`Atipico.Domain/Entities/PedidoMesa.cs`), N:M con `Mesa`
(`Atipico.Domain/Entities/Mesa.cs`, con `Numero` como lo que el personal realmente lee).

- Cero mesas es un estado normal: `Tipo` = `ParaLlevar`/`Delivery` no usa mesa
  (`specs/tipo-pedido.md`), y hasta un pedido `EnSalon` recién creado puede no tener mesa
  asociada todavía.
- Más de una mesa también es normal: `Pedidos/Edit.razor` permite asociar varias ("grupos
  distintos en una mesa grande").

Hoy esa información **solo se ve entrando a editar el pedido** (`Pedidos/Edit.razor`, sección
"Mesas del pedido"). La grilla (`Pedidos/Index.razor`) no la muestra: sus columnas son N°,
Comensal, Estado, Tipo y Creado. Para saber a qué mesa va un pedido "En salón", el mesero o el
cajero tienen que abrirlo — un paso de más para un dato que se usa todo el tiempo cantando
números en el salón, igual que N° o Comensal.

`Repository<TEntity>` (`Atipico.Infraestructure/Persistence/Repositories/Repository.cs`) no
hace `Include`: un `Pedido` cargado desde cualquier lado nunca trae su colección
`PedidoMesas` poblada. `Pedidos/Edit.razor` lo resuelve pidiendo aparte
`IEntityApiClient<PedidoMesa>.GetAllAsync()` y `IEntityApiClient<Mesa>.GetAllAsync()`, y
cruzando en memoria. La grilla necesita el mismo cruce, pero para varios pedidos a la vez.

## 2. Decisión: se resuelve en el backend, dentro de `ArmarGrillaAsync`

Dos formas de obtener el dato en la pantalla, evaluadas:

**A. En el cliente**, como hace `Pedidos/Edit.razor`: agregar a `Pedidos/Index.razor` sus
propias llamadas a `IEntityApiClient<PedidoMesa>` e `IEntityApiClient<Mesa>`, y cruzar ahí.

**B. En el servidor**, extendiendo `GrillaPedidosDto` para que el "sobre" ya traiga la mesa de
cada pedido, calculada en `PedidosController.ArmarGrillaAsync`.

Se elige **B**, por lo mismo que ya explica el comentario de `TotalPedidos` en
`Atipico.Application/Models/TurnoModels.cs`: "el turno va UNA VEZ en el sobre y no repetido en
cada fila". La grilla ya es una sola llamada (`GET api/pedidos/turno-abierto` o
`GET api/pedidos/turno/{id}`) que trae todo lo que la pantalla necesita; sumarle dos llamadas
más de tabla completa (`PedidoMesa`, `Mesa`) desde el cliente reintroduce exactamente el patrón
de N llamadas que la reescritura de la grilla en `specs/numero-pedido.md` §7.3 eliminó para
`Pedido` mismo. Resolverlo en el servidor mantiene "una pantalla, una llamada".

Costo del lado servidor: `PedidosController` ya tiene inyectados `_pedidoMesaService` y
`_mesaService` (los usa `Update` para liberar mesas), así que no hace falta agregar
dependencias nuevas al controlador.

## 3. Modelo de datos: un campo más en el sobre

`GrillaPedidosDto` (`Atipico.Application/Models/TurnoModels.cs`) gana un campo:

```csharp
public sealed record GrillaPedidosDto(
    TurnoDto? Turno,
    IReadOnlyList<Pedido> Pedidos,
    IReadOnlyDictionary<long, IReadOnlyList<int>> MesasPorPedido);
```

`MesasPorPedido` mapea `Pedido.Id` → los números de sus mesas asociadas, ordenados
ascendente. Un pedido sin mesas simplemente no tiene entrada (se consulta con
`GetValueOrDefault`, igual que `_meseroNombres` ya se consulta en `Pedidos/Index.razor`).

**Por qué un diccionario de `int[]` y no un `string` ya formateado ("5, 8").** La pantalla
necesita el número real para poder ordenar la columna (§5.2); si el sobre ya mandara el texto
formateado, ordenar exigiría volver a parsearlo, que es exactamente el error que
`EntityTable.razor` ya documenta evitar: "col.Value devuelve el texto ya formateado [...]
ordenar por esa cadena da resultados falsos". El formateo para mostrar (`string.Join(", ",
...)`) es responsabilidad de la pantalla, no del DTO — mismo reparto que ya existe entre
`Pedido.CreadoEn` (dato crudo) y `p.CreadoEn.ToBoliviaTime().ToString("g")` (formateo en la
columna).

**Por qué no cambiar el tipo de `Pedidos` en el DTO.** La alternativa de envolver cada pedido
en un `PedidoGrillaDto` (con su lista de mesas adentro) se descartó por ser un cambio de
contrato mucho más invasivo sin necesidad: `Pedidos/Index.razor` ya usa `List<Pedido>` en once
lugares distintos (filtros, orden, columnas existentes) y todos seguirían funcionando sin
tocar si el tipo no cambia. Un diccionario aparte, indexado por `Id`, es la misma idea que
`TotalPedidos` (un dato agregado que viaja al lado, no adentro de cada fila) aplicada por
pedido en vez de por turno.

### 3.1 `ArmarGrillaAsync`

```csharp
private async Task<GrillaPedidosDto> ArmarGrillaAsync(TurnoCaja turno)
{
    var pedidos = (await _service.FindAsync(p => p.IdTurnoCaja == turno.Id)).ToList();

    string? cajero = null;
    if (turno.IdCajero is not null)
        cajero = (await _usuarioService.GetByIdAsync(turno.IdCajero.Value))?.Nombre;

    var mesasPorPedido = await MesasPorPedidoAsync(pedidos.Select(p => p.Id));

    return new GrillaPedidosDto(
        new TurnoDto(turno.Id, turno.Nombre, turno.AbiertoEn, turno.CerradoEn, cajero, pedidos.Count),
        pedidos,
        mesasPorPedido);
}

// Una sola pasada por pedido_mesa, filtrada por los pedidos del turno (no toda la tabla:
// FindAsync se traduce a SQL, a diferencia de traer GetAllAsync y filtrar en memoria). Mesa
// sí se trae entera — es una tabla chica que no crece con el tiempo, igual criterio que ya
// usa Pedidos/Edit.razor con MesaApi.GetAllAsync().
private async Task<IReadOnlyDictionary<long, IReadOnlyList<int>>> MesasPorPedidoAsync(IEnumerable<long> idsPedido)
{
    var idsPedidoSet = idsPedido.ToHashSet();
    if (idsPedidoSet.Count == 0)
        return new Dictionary<long, IReadOnlyList<int>>();

    var pedidoMesas = await _pedidoMesaService.FindAsync(pm => idsPedidoSet.Contains(pm.IdPedido));
    var numerosMesa = (await _mesaService.GetAllAsync()).ToDictionary(m => m.Id, m => m.Numero);

    return pedidoMesas
        .GroupBy(pm => pm.IdPedido)
        .ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<int>)g.Select(pm => numerosMesa.GetValueOrDefault(pm.IdMesa))
                                      .OrderBy(n => n)
                                      .ToList());
}
```

Nota: `numerosMesa.GetValueOrDefault(pm.IdMesa)` da `0` si una `PedidoMesa` quedó apuntando a
una mesa borrada — no puede pasar hoy (`app_restaurante` no tiene `GRANT DELETE`, "anular, no
borrar"), así que no se maneja como caso especial; es la misma asunción que ya hace
`Pedidos/Edit.razor` (`_mesas.FirstOrDefault(m => m.Id == pm.IdMesa)?.Numero`).

## 4. Interfaz

### 4.1 Columna nueva: "Mesa"

Se agrega entre **Comensal** y **Estado**. Comensal y Mesa son las dos formas en que el
personal ubica físicamente un pedido en el salón ("Juan, mesa 5"); Estado y Tipo son
metadata operativa que viene después. Columnas resultantes:

`N°, Comensal, Mesa, Estado, Tipo, Creado`

Valor de la columna:

```csharp
("Mesa", p => FormatearMesas(p.Id)),
```

```csharp
// "5", "5, 8" para varias, o "—" sin ninguna. El guion y no vacío: una celda en blanco en
// una tabla se lee como dato faltante por cargar, no como "no aplica" (mismo criterio que
// ya usan otras pantallas para "sin dato").
private string FormatearMesas(long idPedido)
{
    var numeros = _mesasPorPedido.GetValueOrDefault(idPedido);
    return numeros is null || numeros.Count == 0
        ? "—"
        : string.Join(", ", numeros);
}
```

Un pedido `ParaLlevar`/`Delivery` sin mesa muestra "—", igual que uno `EnSalon` al que
todavía no se le asignó ninguna — la columna no distingue esos dos casos, porque tampoco lo
hace hoy la sección "Mesas del pedido" de `Pedidos/Edit.razor` (ambos se leen igual: "sin
mesas asociadas todavía").

### 4.2 Orden

`Pedidos/Index.razor` engancha `OnSort` de `EntityTable`, así que **todas** las columnas se
dibujan con encabezado clicleable (`EntityTable.razor` no ofrece sort parcial) — dejar "Mesa"
sin un caso en el switch de `_pedidosOrdenados` haría que el clic pareciera ordenar por mesa y
en realidad ordenara por Creado (el `default` del switch), que es exactamente el resultado
falso que el comentario de esa misma función ya advierte evitar.

Se agrega:

```csharp
"Mesa" => Aplicar(filtrados, p => _mesasPorPedido.GetValueOrDefault(p.Id)?.FirstOrDefault() ?? 0),
```

Ordena por la mesa **menor** de cada pedido (mismo criterio que ya usa `EntityTable` para
"varios valores, uno para ordenar": no hay una single fuente de verdad mejor cuando hay más
de una mesa). Sin mesa asociada ordena como `0`, es decir **antes** que cualquier mesa real en
ascendente y **después** en descendente — mismo tratamiento simple que ya recibe un
`Comensal` vacío (`p.Comensal ?? ""`, que ordena como cadena vacía sin tratamiento especial de
"siempre al final"); no hay precedente en esta pantalla de tratar los valores ausentes aparte
del orden natural de su tipo.

"Mesa" **no** entra en la cadena de desempate (`ThenByDescending`/`ThenBy` después del
switch), por el mismo motivo que ya excluye a "N°": popular mesas no cambia el desempate entre
pedidos que ya comparten Creado/Estado/Comensal de forma útil, y sumarlo sería un criterio
más para mantener sin que aporte nada.

### 4.3 Rol y visibilidad

Sin cambios de roles. La columna se ve en la grilla igual que el resto — no hay motivo para
esconderle al Mesero a qué mesa corresponde un pedido; es al revés, es quien más la necesita
para llevar el plato.

## 5. Plan de implementación

| # | Capa | Archivo | Cambio |
|---|---|---|---|
| 1 | Application | `Atipico.Application/Models/TurnoModels.cs` | agregar `MesasPorPedido` a `GrillaPedidosDto` (§3) |
| 2 | Api | `Atipico.Api/Controllers/PedidosController.cs` | `ArmarGrillaAsync` + `MesasPorPedidoAsync` (§3.1) |
| 3 | Web | `Components/Pages/Pedidos/Index.razor` | campo `_mesasPorPedido`, columna "Mesa" (§4.1), caso de orden (§4.2) |
| 4 | Pruebas | `Atipico.Api.Tests/PedidosControllerTurnoTests.cs` | ver §6 |

No hay migración SQL: no se toca el esquema, solo se cruzan datos que ya existen.

## 6. Plan de pruebas

`PedidosControllerTurnoTests` construye `PedidosController` con
`new Mock<IEntityService<PedidoMesa>>().Object` y `new Mock<IEntityService<Mesa>>().Object`
sin ningún `Setup` — hoy da igual porque `ArmarGrillaAsync` no los toca. En cuanto los toque,
esos mocks sin `Setup` devuelven `null` en vez de una lista vacía y `MesasPorPedidoAsync`
revienta con `NullReferenceException`. Hace falta:

1. En el constructor del test, agregar el `Setup` por defecto que hoy falta:
   ```csharp
   _pedidoMesas.Setup(s => s.FindAsync(It.IsAny<Expression<Func<PedidoMesa, bool>>>())).ReturnsAsync([]);
   _mesas.Setup(s => s.GetAllAsync()).ReturnsAsync([]);
   ```
   (nuevos campos `Mock<IEntityService<PedidoMesa>> _pedidoMesas` y
   `Mock<IEntityService<Mesa>> _mesas`, reemplazando los `new Mock<...>().Object` inline).
2. Casos nuevos a cubrir:
   - Pedido sin `PedidoMesa` asociada → `MesasPorPedido` no trae entrada para su `Id` (o trae
     lista vacía; se decide al implementar cuál de las dos formas es más simple para el
     consumidor y se documenta acá).
   - Pedido con una mesa → `MesasPorPedido[id]` es `[numero]`.
   - Pedido con dos mesas → vienen ordenadas ascendente sin importar el orden de inserción.
   - Dos pedidos del turno, cada uno con su propia mesa → no se cruzan entre sí (el filtro por
     `idsPedidoSet` es correcto).
3. En `Atipico.Web`, si existen pruebas de `Pedidos/Index.razor` (a día de escribir esto no se
   encontraron con ese nombre — verificar `Atipico.Api.Tests`/`*.Tests` antes de descartarlo),
   agregar un caso para `FormatearMesas` y para el nuevo `case "Mesa"` del switch de orden.

## 7. Bitácora

- **2026-09-05.** Análisis inicial a partir de SCRUM-16 (sin descripción en el ticket). Se
  investigó el modelo `Pedido`↔`Mesa` (N:M vía `PedidoMesa`), se confirmó que
  `Repository<TEntity>` no hace `Include` (por eso la asociación no llega gratis a ningún
  lado), y se decidió resolver el cruce en el servidor dentro de `ArmarGrillaAsync` en vez de
  agregar llamadas nuevas desde `Pedidos/Index.razor`, según el criterio ya sentado por
  `TotalPedidos` en el mismo DTO. Pendiente de aprobación antes de tocar código.
