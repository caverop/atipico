---
name: atipico-runbook-y-verificacion
description: "Pruebas de desarrollo (usuario y agente) todas locales en Docker. La regla de Neon —nunca, salvo pedido explícito del usuario en el momento— vive en la memoria del agente db, que es quien interactúa con la base."
metadata:
  type: feedback
---

Cuando un cambio necesita correr contra una instancia real, el agente **entrega un
documento con los pasos a seguir** y el usuario **ejecuta**. Pedido explícito el
2026-09-10, como "forma de trabajo" — no es para una migración puntual, aplica siempre.

**El patrón:** script + runbook con comandos exactos y probados (no redactados de
memoria) + consulta de verificación posterior con el resultado esperado escrito al lado
— el mismo entregable de tres partes que `specs/agente-db.md` §2.3 ya exigía para
migraciones, extendido a cualquier script contra una instancia real.

**Dónde corre cada verificación:**

- **La instancia descartable del agente (siempre).** Contenedor `postgres:18-alpine`,
  nace de la cadena canónica, se borra al terminar. `Atipico.Database.Tests`
  (Testcontainers) hace esto mismo de forma permanente.
- **La instancia local persistente del usuario** (`docker-compose.db.yml`,
  `localhost:5433`, [[atipico-postgres-local-dev]]). Local, sin credencial de Neon de por
  medio.
- **`qa` y `production` en Neon: el agente no se conecta, bajo ningún concepto, salvo
  pedido explícito del usuario en ese momento.** El detalle completo de esta regla — el
  porqué, las frases textuales con que se llegó ahí, y cómo aplicarla — se movió a
  `.claude/agent-memory/db/db-nunca-neon-salvo-pedido-explicito.md` el 2026-09-10, a
  pedido del usuario: es el agente `db` quien interactúa con la base, así que es ahí
  donde tiene que estar. Si estás trabajando DB sin haber invocado a `db`, leé esa nota
  igual — la regla aplica a cualquier agente que toque `sql/` o una conexión real.

**Trampa: `dotnet user-secrets list` no tiene forma de ocultar valores.** Cualquier
llamada que lo corra sin capturar la salida imprime la cadena de conexión completa
—contraseña incluida— en la transcripción. La forma correcta es capturar directo a una
variable de shell en el mismo comando que la usa, nunca un paso previo que solo "liste
para ver si está": `CONN=$(dotnet user-secrets list --project X | grep '^Clave' | sed
's/^Clave = //')`, nunca imprimir `$CONN`. Mismo cuidado al armar
`PGPASSWORD`/`PGUSER`/etc. para `psql`: derivarlos de la variable capturada, no de un
`echo` intermedio — y ojo con un `sed` que solo tapa una línea: dejó pasar ocho secretos
más (R2, JWT, Aspire) una vez.

Relacionado: [[atipico-postgres-local-dev]], [[atipico-grafo-con-el-commit]].
