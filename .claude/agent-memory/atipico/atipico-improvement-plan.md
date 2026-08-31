---
name: atipico-improvement-plan
description: "Plan de mejoras de 13 tareas COMPLETADO (2026-08-14) — se conserva por los tradeoffs que registra, sobre todo el alcance real de app_restaurante y los botones Quitar que ya no pueden funcionar."
metadata:
  type: project
---

> Recortado al migrar (2026-08-31): la descripción de qué hace cada tarea terminada ya
> vive en `CLAUDE.md` (TryTranslateDbError, ApiException, ReportesController, vistas
> keyless, sincronización enum↔CHECK, etc.) y no se duplica acá. Queda lo que
> `CLAUDE.md` **no** dice: por qué se decidió así, qué quedó roto a propósito, y los
> hallazgos que costaría volver a derivar.

Atipico tuvo una iniciativa de mejoras multi-sesión que el usuario pidió ejecutar
tarea por tarea después de una revisión de `sql/script_inicial.sql` (esquema,
triggers, vistas) cruzada contra el código C#. **Las 13 tareas están hechas y
verificadas en vivo contra la API y PostgreSQL reales al 2026-08-14.** No hay nada
pendiente; esta nota no es para retomar trabajo.

El principio de timestamps que salió de la tarea 1 está en
[[atipico-server-stamped-timestamps]].

## `app_restaurante` tuvo un radio de impacto mayor que su propia tarea

Cambiar la conexión al rol de bajo privilegio revoca DELETE en **todas** las tablas
por igual — no solo `Cuenta`/`DetalleCuenta`. El comentario del propio esquema dice
"Sin DELETE: los borrados se hacen anulando, no eliminando filas" como política de
todo el rol, así que ésta fue la implementación fiel, no scope creep.

Consecuencia descubierta al verificar en vivo: los botones "Quitar mesa" / "Quitar
plato" de `Pedidos/Edit.razor` (construidos en una sesión anterior, funcionando
entonces) ya **nunca pueden tener éxito** — `DELETE /api/pedido-mesas/{id}` da 409 a
nivel de rol antes de que corra la lógica de liberación de mesa de
`PedidoMesasController`. Es elegante (el manejo de `ApiException` muestra un mensaje
claro, no crashea) pero los botones ya no *funcionan*: solo explican por qué no
pueden. Verificado el 2026-08-31 que siguen ahí (`Edit.razor` líneas ~97 y ~248).

**Se dejó así deliberadamente:** rediseñar la estructura de GRANTs (p. ej. devolver
DELETE sobre tablas no financieras como `pedido_mesa`) iba más allá de "conectar el
rol como lo define el esquema", que era lo pedido. Si en el futuro piden restaurar
esa funcionalidad, las opciones son (a) devolver DELETE sobre tablas puntuales no
auditables, o (b) cambiar esas dos features a un patrón de "anular", consistente con
el resto de la app. **Plantear el tradeoff explícitamente en vez de elegir en
silencio.**

**BLOQUE 2 de `script_inicial.sql` (el rol) nunca se había corrido** en esa DB — solo
BLOQUE 1 (esquema). Hubo que correrlo a mano y después `ALTER ROLE app_restaurante
WITH PASSWORD`. Lo mismo volvió a pasar en Neon después ([[atipico-neon-deployment]]):
si el rol se recrea, hay que rehacer los grants y actualizar la cadena de conexión.

## Un bug real introducido y arreglado en el camino

Agregar el efecto colateral de estado de mesa en `PedidoMesasController.Create`
(cargar `Mesa` vía `_mesaService.GetByIdAsync` en el mismo request/DbContext justo
después de agregar el `PedidoMesa`) hizo que EF Core arreglara las navegaciones
bidireccionales `Mesa.PedidoMesas` ↔ `PedidoMesa.Mesa`, produciendo un ciclo de
serialización: `POST /api/pedido-mesas` devolvía un 500 crudo aunque las escrituras
a la DB habían funcionado. Se arregló de forma general, no con un parche local, con
`options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;` en
el `AddJsonOptions` de `Atipico.Api/Program.cs` (verificado presente el 2026-08-31,
línea 53) — protege contra la misma clase de bug en cualquier otro controlador que
cargue una entidad relacionada trackeada antes de serializar.

## Detalle de implementación que no está en CLAUDE.md

La liberación de mesa (`LiberarMesaSiNoTieneOtroPedidoActivoAsync`) está **duplicada**
en `PedidoMesasController` y `PedidosController` a propósito, en vez de extraída a un
servicio compartido: era lo bastante chica como para que la extracción no valiera la
pena. `mesa.estado` transiciona Libre→Ocupada y Ocupada→Libre, y nunca toca
Reservada/Inactiva (estados manuales).

## Contaminación de datos de prueba (inofensiva, no es una regresión)

`GET /api/reportes/cuentas-descuadradas` devuelve filas de sesiones de prueba viejas
("Cliente Auth Test", "TEST-Grupo Norte", "TEST-Verif Fix2") cuyo `Monto` nunca se
concilió contra la suma de su `detalle_cuenta` — confirmado por SQL directo (dos
llevan el `DateTimeOffset` por defecto `0001-01-01`, señal de filas insertadas por
scripts de prueba y no por el flujo real). Se dejaron: las cuentas no se pueden
borrar, y son una buena prueba viva de que la vista de conciliación funciona.

Relacionado: [[atipico-narrow-enum-over-migration]] (el drift de `ck_cuenta_metodo`
salió de esta revisión), [[atipico-local-postgres-tooling]].
