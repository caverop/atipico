---
name: atipico-server-stamped-timestamps
description: "Todo timestamp de transición de estado (servido/cerrado/pagado/anulado) se calcula en el servidor, nunca es un input que el usuario tipea."
metadata:
  type: feedback
---

Cuando un campo de fecha registra "cuándo ocurrió esta transición de estado"
(`ServidoEn`, `CerradoEn`, `PagadoEn`, `AnuladoEn` en `Pedido`/`PedidoPlato`/`Cuenta`),
calcularlo en el servidor con `DateTimeOffset.UtcNow` en el momento en que se detecta
la transición — nunca exponerlo como input editable en la UI. Mostrarlo de solo
lectura una vez seteado.

**Why:** el `InputDate` de Blazor bindeado a `DateTimeOffset` arma el valor con el
offset local del navegador (`-04:00` en Bolivia). Npgsql solo acepta escrituras con
offset 0 (UTC) a columnas `timestamptz`, así que cualquier timestamp mandado por el
cliente rompía la API con un 500 (`System.ArgumentException: Cannot write
DateTimeOffset with Offset=-04:00:00...`). Más allá del crash, el usuario dijo
explícitamente (2026-08-12) que es mejor diseño que estos campos sean *"manejados
internamente y no una opción para que el usuario digite"* — independientemente del
bug. Confirmado como la decisión correcta: quedó como patrón en los tres
controladores afectados.

**How to apply:** al agregar o tocar un campo de timestamp de transición:
(1) nada de `InputDate` ni control editable en el formulario, solo un
`<p class="form-control-plaintext">` de solo lectura cuando tiene valor;
(2) el `Update` del controlador detecta la transición
(`existing.Estado != entity.Estado && entity.Estado == X`) y estampa
`DateTimeOffset.UtcNow` él mismo, ignorando lo que haya mandado el cliente. Esto
obligó a sobrescribir `Update` en `PedidosController`/`PedidoPlatosController`/
`CuentasController` con el patrón fetch-and-mutate sobre la entidad trackeada (el
mismo que `UsuariosController` ya usaba para hashear la contraseña) en vez de
adjuntar el grafo completo del cliente vía `EntityControllerBase.Update`.

Nota: para *mostrar* esos UTC está `ToBoliviaTime()` — ver `CLAUDE.md`, no
`ToLocalTime()`, que en Blazor Server resuelve a la zona del servidor.
