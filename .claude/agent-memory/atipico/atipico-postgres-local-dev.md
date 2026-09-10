---
name: atipico-postgres-local-dev
description: "dev deja de ser un branch de Neon: pasa a Docker local (usuario, persistente, puerto 5433) y contenedor descartable (agente, sin cambios). SCRUM-29, specs/postgres-local-dev.md."
metadata:
  type: project
---

Desde el 2026-09-10 (SCRUM-29, `specs/postgres-local-dev.md`, `propuesto`), `dev` deja de
ser un branch de Neon y pasa a vivir **solo local**: Docker persistente para el usuario
(`docker-compose.db.yml`, puerto **5433** — el 5432 ya lo ocupa el PostgreSQL nativo de
Windows, ver [[atipico-local-postgres-tooling]]) y contenedor descartable para el agente,
que no cambia respecto de lo que `specs/agente-db.md` ya hacía.

**Why:** el mismo día, verificando `sql/015_cuenta_metodo_qr.sql`, `dotnet user-secrets
list` expuso la cadena de conexión de Neon completa (con contraseña) en la conversación, y
la consulta de verificación que siguió falló por autenticación. En vez de depurar el
fallo puntual, el usuario decidió sacar `dev` de Neon del todo — nadie necesita tocar una
credencial de una base compartida para el trabajo cotidiano.

**`qa` y `production` no cambian, siguen en Neon.** Lo que cambia es que ahora hay un
orden explícito de tres gates manuales antes de que una migración llegue a producción:
**local (usuario) → `qa` (usuario) → `production` (usuario)**, con el agente entregando
migración + runbook probado en su propio contenedor descartable como primer paso, nunca
tocando Neon.

**How to apply:** cuando el trabajo toque la base de desarrollo, es la instancia local
—no Neon—. `specs/neon-branches-ambientes.md` §2 (fila `dev`), §4, §5 y las partes de §6
sobre `dev` quedan marcadas *superseded* por este spec, sin reescribirse — la historia de
por qué `dev` fue Neon primero queda como registro. Va junto con
[[atipico-runbook-y-verificacion]], la forma de trabajo que se fijó el mismo día para
cualquier script contra una instancia real.

**Pendiente, decisión del usuario:** si se borra el branch `dev` de Neon (ya sin uso, no
urgente) y si se rota la contraseña de `app_restaurante` que se filtró — ver §9 y §8.1 del
spec.

Relacionado: [[atipico-runbook-y-verificacion]], [[atipico-local-postgres-tooling]],
[[atipico-neon-deployment]].
