# Tipo de pedido: en salón, para llevar y delivery

Especificación funcional y técnica para distinguir cómo se consume un pedido. Documento de
referencia previo a la implementación: recoge las decisiones tomadas y su porqué, para no
volver a discutirlas al escribir el código.

- **Estado:** **implementado**, compila y con pruebas en verde. Falta correr la migración
  contra producción (§9) y probarlo con datos reales.
- **Alcance:** clasificar el pedido en uno de tres tipos, y poder filtrar y ordenar por eso.
  Nada más.
- **Fuera de alcance:** todo lo operativo del delivery — dirección, teléfono, costo de envío,
  repartidor. `DELIVERY` entra **solo como etiqueta** (§7).

---

## 1. Contexto: la mesa ya es opcional

El punto de partida importa porque cambia el problema. Un pedido **sin mesa ya es válido
hoy**, en la base y en la aplicación:

- No hay constraint ni trigger que exija una fila en `pedido_mesa`.
- `PedidosController` factura sin mirar mesas: cerrar el pedido solo exige método de pago.
- `Pedidos/Edit.razor` ya muestra «Sin mesas asociadas todavía» como estado normal.
- Ninguna vista (`v_pedido_plato_sin_cobrar`, `v_cuenta_descuadrada`, las de comprobantes)
  agrupa ni filtra por mesa.

Es decir: **un pedido para llevar ya se puede registrar hoy**, simplemente no asociando mesa.
Lo único que falta es poder *decirlo*, para distinguir «es para llevar» de «el mesero se
olvidó de asignar la mesa».

Eso es un dato del pedido, no un lugar.

## 2. Decisión de fondo: una columna en `pedido`, no una mesa fantasma

La alternativa evaluada fue crear una fila en `mesa` llamada «Para llevar» y asociar ahí los
pedidos, sin tocar el esquema. Se descartó por cinco razones concretas.

**El id no es portable.** `mesa.id` es `GENERATED ALWAYS AS IDENTITY`. La mesa fantasma
tendría un id en desarrollo y otro distinto en producción. Cualquier consulta de «cuánto
vendimos para llevar» quedaría atada a un número mágico, configurable por ambiente o
resuelto por `numero`. Con una columna es `WHERE tipo = 'PARA_LLEVAR'` en todos lados.

**Nada la protege.** Aparecería en la pantalla de Mesas como una mesa más. `Mesas/Index.razor`
permite marcarla `Inactiva` con un clic, y `_mesasDisponibles` filtra las inactivas: «para
llevar» desaparecería sin que nadie entienda por qué.

**Obliga a inventar datos.** `numero` es `UNIQUE` e `int`, y `ck_mesa_cap` exige
`capacidad > 0`. Habría que asignarle un número y una capacidad que no significan nada.

**El estado queda sin sentido.** `PedidoMesasController` marca `OCUPADA` al asociar, y
`LiberarMesaSiNoTieneOtroPedidoActivoAsync` libera al cerrarse el último pedido activo. Diez
pedidos para llevar simultáneos «ocuparían» una mesa de capacidad 1, y sin ninguno la mesa
«Para llevar» figuraría `LIBRE`.

**No escala a tres opciones.** Con delivery habría que inventar una segunda mesa fantasma, y
el problema se duplica.

## 3. Modelo de datos

### 3.1 El enum

`Atipico.Domain.Enums.TipoPedido`, con la convención de siempre: PascalCase en C#,
`UPPER_SNAKE_CASE` en la base vía `UpperSnakeCaseEnumConverter`.

| C# | Base | Etiqueta en pantalla |
|---|---|---|
| `EnSalon` | `EN_SALON` | En el restaurante |
| `ParaLlevar` | `PARA_LLEVAR` | Para llevar |
| `Delivery` | `DELIVERY` | Delivery |

Sobre `EnSalon`: el valor en base dice «salón» aunque la etiqueta diga «restaurante». Es
deliberado — `tipo = 'EN_RESTAURANTE'` dentro de la base de un restaurante no aporta nada, y
«salón» es el término del rubro. La etiqueta es lo que ve el mesero, y ahí sí manda la
palabra del usuario.

La columna se llama `tipo`, sin prefijo, igual que `pedido.estado`, `mesa.estado` y
`plato.estado`.

### 3.2 DDL

```sql
-- sql/008_pedido_tipo.sql
BEGIN;

-- Postgres 11+ llena las filas existentes con el DEFAULT sin reescribir la tabla, asi que
-- no hace falta el backfill en dos pasos de 004_plato_estado_habilitado.sql. Todos los
-- pedidos que ya existen fueron consumidos en el salon: EN_SALON es el valor correcto para
-- ellos, no solo un relleno.
ALTER TABLE pedido
    ADD COLUMN tipo varchar(20) NOT NULL DEFAULT 'EN_SALON';

-- Espejo de Atipico.Domain.Enums.TipoPedido, en sync a mano (ver el comentario de
-- mantenimiento al inicio de script_inicial.sql).
ALTER TABLE pedido
    ADD CONSTRAINT ck_pedido_tipo CHECK (tipo IN ('EN_SALON', 'PARA_LLEVAR', 'DELIVERY'));

COMMIT;
```

**No rompe ningún trigger.** `pedido` solo tiene `tg_touch_pedido`, un `BEFORE UPDATE` que
fija `actualizado_en`. No hay nada como `fn_cuenta_inmutable`, que compara `to_jsonb(OLD)`
contra `to_jsonb(NEW)` y por eso sí se ve afectado por cualquier columna nueva.

**No hace falta índice.** Tres valores sobre una tabla chica: el planificador preferirá un
seq scan igual, y el filtrado de `Pedidos/Index.razor` ocurre en memoria.

## 4. Qué NO se restringe, y por qué

**El tipo no obliga ni prohíbe mesas.** No hay trigger que impida asociar una mesa a un
pedido `PARA_LLEVAR`, ni que exija una en `EN_SALON`. Dos motivos:

1. **Violarlo no corrompe nada.** Ningún reporte ni cálculo depende de esa combinación. La
   filosofía del proyecto es poner en la base las reglas cuya violación deja datos
   inconsistentes, y esta no es una de ellas.
2. **El caso mixto existe de verdad.** Un comensal sentado espera en la mesa y se lleva
   parte; un pedido de delivery se arma sobre una mesa mientras se empaca. Prohibirlo
   obligaría a mentir sobre el tipo.

La consecuencia es que **la interfaz guía y la base no castiga** (§5).

**`uk_pedido_comensal_activo` sigue igual.** Dos pedidos `ABIERTO`/`EN_PREPARACION` no pueden
compartir comensal, y el tipo no entra en ese índice. Si «Juan» come en el salón y además
pide uno para llevar como pedido aparte, hay que distinguirlo en el nombre. Es una
restricción preexistente: el tipo de pedido no la agrava ni la mejora.

## 5. Interfaz

**`Pedidos/Edit.razor`** — un `<select>` con los tres tipos, junto al comensal, arriba de
todo. Es un dato que se sabe antes de tomar el primer plato.

La sección de mesas se comporta así:

| Situación | Sección de mesas |
|---|---|
| `EnSalon` | visible, como hoy |
| `ParaLlevar` / `Delivery`, sin mesas asociadas | oculta |
| `ParaLlevar` / `Delivery`, **con** mesas ya asociadas | visible, con aviso |

La tercera fila es la que importa: si el mesero cambia el tipo después de haber asociado una
mesa, **la asociación no se borra ni se esconde**. Ocultarla dejaría una mesa ocupada de
forma invisible, que solo se liberaría al cerrarse el pedido y que nadie podría diagnosticar.
Se muestra con un texto que invita a quitarla, y decide la persona.

**`Pedidos/Index.razor`** — una columna «Tipo» y un filtro `<select>` junto al de Estado. La
columna entra en el ordenamiento que ya existe (`OnSort` de `EntityTable`); no se suma al
orden por defecto, que sigue siendo Creado → Estado → Comensal.

**Nada más cambia.** El flujo de facturación, los comprobantes QR y los estados del pedido
son indiferentes al tipo.

### 5.1 Una columna nueva se copia en DOS lugares, o no se guarda

Aparecido al implementar, y no es evidente, porque son dos copias encadenadas y cada una
falla distinto:

**En la API.** `PedidosController.Update` **no vuelca la entidad recibida**: carga la fila con
`GetByIdAsync` y copia campo por campo (`Comensal`, `Estado`, `Tipo`, `IdMesero`). Una columna
que falte en esa lista es una columna que **ningún PUT puede modificar jamás** — se guarda al
crear y queda congelada. No hay error: el PUT responde 200 y el cambio desaparece.

**En la web.** `HandleCerrarPedido`, `HandleMarcarServido` y `HandleAnularPedido` no envían
`_entity`: arman un `Pedido` nuevo copiando campo por campo. Una columna que falte ahí viaja
en su valor por defecto — y ahora que la API sí copia `Tipo`, **eso la pisa en la base**: cada
pedido para llevar volvería a `EN_SALON` al pasar a En Preparación.

Las dos copias se necesitan mutuamente. Con solo la de la web, el tipo no se puede editar;
con solo la de la API, se pierde en cada transición. Lo mismo le va a pasar a la próxima
columna que se agregue a `pedido` — la dirección de entrega, por ejemplo.

## 6. API y roles

**Sin cambios.** `PedidosController` extiende `EntityControllerBase<Pedido>`: el alta y la
edición serializan la entidad completa, así que la columna viaja sola en cuanto está en el
dominio y en la configuración de EF.

Los roles tampoco cambian: quien puede crear o editar un pedido puede fijar su tipo. No hay
motivo para que un mesero pueda registrar un pedido pero no decir si es para llevar.

## 7. Delivery: por ahora, solo la etiqueta

`DELIVERY` entra como un valor más del enum y nada más. **No se agrega nada relacionado al
delivery**: ni dirección, ni teléfono, ni costo de envío, ni repartidor. Un pedido por
delivery se registra hoy exactamente igual que uno para llevar, y lo único que cambia es cómo
queda clasificado — que es lo que se pidió: poder contarlos y filtrarlos.

Queda una advertencia anotada para cuando llegue el momento de operarlo de verdad, porque no
es evidente y sería caro descubrirla tarde: **el costo de envío no es una columna más.**
`v_cuenta_descuadrada` marca toda cuenta donde `cuenta.monto` difiera de
`SUM(detalle_cuenta.precio_unitario)`, así que sumar el envío a `cuenta.monto` haría aparecer
**cada pedido por delivery como descuadrado**. Hay al menos tres salidas — el envío como un
`detalle_cuenta` más apuntando a un plato «Envío», una columna aparte en `cuenta` con la
vista ajustada, o cobrarlo fuera del sistema — y cada una pega distinto en los reportes.

Dirección y teléfono, en cambio, son columnas anulables sin efecto en ninguna vista: el día
que hagan falta se agregan con una migración trivial. Nada de esto bloquea lo de acá.

## 8. Plan de implementación

| # | Capa | Archivo | Cambio |
|---|---|---|---|
| 1 | SQL | `sql/008_pedido_tipo.sql` | el DDL de §3.2 |
| 2 | Dominio | `Atipico.Domain/Enums/TipoPedido.cs` | enum nuevo |
| 3 | Dominio | `Atipico.Domain/Entities/Pedido.cs` | `TipoPedido Tipo`, con default `EnSalon` |
| 4 | Infra | `Persistence/Configurations/PedidoConfiguration.cs` | mapear con `UpperSnakeCaseEnumConverter` |
| 5 | Web | `Components/Pages/Pedidos/Edit.razor` | select de tipo; mesas según §5 |
| 6 | Web | `Components/Pages/Pedidos/Index.razor` | columna, filtro y orden |
| 7 | Web | `Atipico.Web/TipoPedidoExtensions.cs` | `Etiqueta()`, la traducción enum → texto, en un solo lugar |
| 8 | Pruebas | `Atipico.Infraestructure.Tests/ModeloTipoPedidoTest.cs` | ida y vuelta del enum contra los literales del `CHECK` |

El último paso es el que evita el modo de falla conocido de este proyecto: el enum de C# y el
`CHECK` de la base se mantienen en sync **a mano**, y una divergencia no se nota hasta que un
`INSERT` falla en producción. La prueba **lee `sql/008_pedido_tipo.sql`** y extrae los
literales del `CHECK` con una expresión regular, en vez de copiarlos: copiarlos habría creado
un tercer lugar que también puede quedar desactualizado.

## 9. Puesta en producción

1. Correr `sql/008_pedido_tipo.sql` contra la base de producción. Es una sola transacción y
   no reescribe la tabla.
2. Verificar que la columna quedó `NOT NULL` con `DEFAULT 'EN_SALON'`, y que todas las filas
   existentes tomaron ese valor:
   ```sql
   SELECT tipo, count(*) FROM pedido GROUP BY tipo;
   ```
3. No hay `GRANT` nuevo que dar: `app_restaurante` ya tiene `SELECT, INSERT, UPDATE` sobre
   `pedido`, y los permisos son por tabla, no por columna.
4. No hay variables de entorno ni configuración nueva.

**Nota de higiene, aparte de esto:** `sql/schema_completo.sql` es un consolidado que quedó en
`005`. No incluye `006_comprobante_pago.sql` ni `007_comprobante_monto_opcional.sql`. Conviene
ponerlo al día en algún momento; no bloquea esta funcionalidad, pero cada migración que pasa
lo aleja más de ser útil.
