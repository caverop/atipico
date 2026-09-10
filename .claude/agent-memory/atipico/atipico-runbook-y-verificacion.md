---
name: atipico-runbook-y-verificacion
description: "Para cualquier script que toque una instancia real (local persistente o Neon), el agente entrega un documento con los pasos exactos y verifica con las consultas necesarias; el usuario ejecuta en sus instancias."
metadata:
  type: feedback
---

Cuando un cambio necesita correr contra una instancia real —la local persistente del
usuario, `qa` o `production` en Neon—, el agente **entrega un documento con los pasos a
seguir**, el usuario **ejecuta** en sus instancias, y el agente **valida con las
consultas necesarias**. Pedido explícito el 2026-09-10, como "forma de trabajo" — no es
para una migración puntual, aplica siempre.

**Why:** es la generalización de lo que `specs/agente-db.md` ya exigía para migraciones
—script + runbook probado + consulta de verificación con el resultado esperado
escrito al lado— llevado a *cualquier* script que toque una instancia real, no solo
`sql/NNN_*.sql`. Surgió el mismo día en que [[atipico-postgres-local-dev]] se decidió:
después de exponer una credencial de Neon y fallar una consulta contra ella, quedó claro
que "yo entrego, vos corrés, yo confirmo" tiene que ser el patrón por defecto, no una
excepción para SQL.

**How to apply — quién verifica qué, y contra qué:**

- **La instancia descartable del agente (siempre).** El agente arma el runbook
  reproduciéndolo primero ahí — contenedor `postgres:18-alpine`, nace de la cadena
  canónica, se borra al terminar. Esto no cambia: es la base de todo runbook, como ya
  se hizo con `sql/015_cuenta_metodo_qr.sql`.
- **La instancia local persistente del usuario (una vez que exista, ver
  [[atipico-postgres-local-dev]]).** Es local, sin credencial de Neon de por medio — el
  agente puede correr ahí las consultas de verificación directamente después de que el
  usuario confirme que aplicó el script, sin el riesgo que motivó todo esto.
- **`qa` y `production` en Neon.** El agente **puede conectarse de solo lectura** —
  `SELECT` y `pg_dump --schema-only`, la excepción que ya fijaba `agente-db.md` §2.2 y que
  el usuario confirmó explícitamente el 2026-09-10 tras una primera versión de
  [[atipico-postgres-local-dev]] que la eliminaba de más. Nunca DDL ni DML: eso sigue
  siendo exclusivo del usuario, a mano, cuando decide. La consulta de verificación va
  igual en el runbook con el resultado esperado, pre-probada en el contenedor
  descartable — la diferencia es que el agente **también** puede correrla él mismo contra
  la base real después de que el usuario aplicó el script, no solo dejarla escrita.
  **Al conectar, nunca imprimir la credencial en la transcripción** — ver la trampa de
  abajo.

**El documento de pasos** sigue el patrón de tres entregables que ya usa `agente-db.md`:
el script, el runbook con comandos exactos y probados (no redactados de memoria), y la
consulta de verificación posterior con el resultado esperado. No es nuevo formato — es
extender ese patrón fuera de las migraciones SQL.

**Trampa: `dotnet user-secrets list` no tiene forma de ocultar valores.** Cualquier
llamada que lo corra sin capturar la salida imprime la cadena de conexión completa
—contraseña incluida— en la transcripción. Pasó el 2026-09-10 verificando `015`. La forma
correcta es capturar directo a una variable de shell en el mismo comando que la usa, sin
un paso previo que solo "liste para ver si está": `CONN=$(dotnet user-secrets list
--project X | grep '^Clave' | sed 's/^Clave = //')`, nunca imprimir `$CONN`. Mismo cuidado
al armar `PGPASSWORD`/`PGUSER`/etc. para `psql`: derivarlos de la variable capturada, no
de un `echo` intermedio.

Relacionado: [[atipico-postgres-local-dev]], [[atipico-grafo-con-el-commit]].
