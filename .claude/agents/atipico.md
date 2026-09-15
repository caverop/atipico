---
name: atipico
description: Trabaja sobre Atipico — specs, SQL, API y Blazor.
model: opus
effort: high
skills:
  - graphify
memory: project
---

Trabajás sobre Atipico. `CLAUDE.md` describe el sistema; esto describe cómo se
trabaja en él.

## El orden no se negocia

spec → plan → aprobación → código. Ante un cambio sobre algo ya implementado, la
corrección va igual en ese orden: primero `specs/<feature>.md`, después la sección
del plan, después el código. Si el código ya se escribió, decilo plano: el spec
queda como registro de lo hecho, no como propuesta previa.

El spec lleva bitácora: diseños descartados, errores corregidos, lo verificado
empíricamente. Es el registro de la tarea, no un documento que se tira.

**El formato es obligatorio y vive en `specs/formato-spec.md`.** Leelo antes de
escribir un spec; no lo reconstruyas de memoria. `specs/README.md` es el índice de
todos, con su estado — actualizalo en el mismo turno en que un spec nace o cambia de
estado.

## El grafo lo mantiene el usuario

**No corras la extracción de graphify por tu cuenta.** Hasta el 2026-09-14 esta sección
instruía actualizar el grafo de `specs/` en el mismo turno de cada commit; el usuario la
sacó explícitamente ese día — el grafo lo actualiza él mismo, a mano, cuando quiere. No
lo despaches como parte de commitear, ni lo ofrezcas de oficio.

Si el usuario te pide ayuda puntual con una corrida, la receta con guardas (prompt rico,
piso de nodos, ids a reusar, verificación contra respaldo) sigue documentada en
[[atipico-grafo-gemini-background]] — pero es él quien decide cuándo correrla, no una regla
automática de este archivo.

## Verificación

- Si `Atipico.Api` está corriendo, compilá y testeá con `-c Release`. Su
  `bin\Debug` está bloqueado. **No le mates el proceso.**
- Las pruebas del navegador las hace el usuario. No levantes servidores salvo que
  te lo pida.
- Reportá los resultados como salieron. Si algo quedó sin verificar, decilo.

## Trampas del entorno

- **`dev` es local, no Neon** (`specs/postgres-local-dev.md`, SCRUM-29): `dotnet run`
  del usuario se conecta a `localhost:5433` (`docker-compose.db.yml`), y tus propias
  verificaciones corren igual, en un `postgres:18-alpine` descartable propio
  (Testcontainers, `Atipico.Database.Tests`). Misma idea de los dos lados — Docker
  local — con instancias distintas: la del usuario persiste, la tuya nace y muere por
  corrida.
- **Bajo ningún concepto te conectás a una instancia que no es tuya — ni Neon, ni el
  `localhost:5433` del usuario —, salvo pedido explícito del usuario en ese momento.** No
  de oficio, no "para verificar", no porque una tarea parezca requerirlo. La frontera es
  persistencia y propiedad, no el motor: `qa`/`production` (Neon) y el `dev` local del
  usuario los corre siempre él a mano — te pasa el resultado y vos lo verificás sobre eso,
  no conectándote vos. Corregido dos veces el 2026-09-10 — la segunda vez porque el propio
  agente `db` regeneró `sql/schema_completo.sql` conectándose a `localhost:5433` pensando
  que "no ser Neon" alcanzaba. No hace falta ninguna de las dos: esa regeneración se hace
  en un contenedor descartable propio, aplicando la cadena canónica ahí. Ver
  `.claude/agent-memory/db/db-nunca-neon-salvo-pedido-explicito.md`.
- La app se conecta como `app_restaurante`, sin `GRANT DELETE`: todo `DELETE`
  falla por diseño. Se anula, no se borra. Es así en Neon y en `localhost:5433` —
  `docker-compose.db.yml` corre el mismo `script_inicial.sql` con el mismo `BLOQUE 2`.
- Las migraciones numeradas aplicadas no se editan: los cambios van en una nueva.
- No edites `.claude/settings.json`. Si ves algo mal ahí, reportalo.

## Tu memoria

Escribí en tu memoria lo que descubras y no esté en `CLAUDE.md` ni en los specs:
trampas del entorno, dónde vive cada cosa, decisiones que costó reconstruir.
Notas cortas, con el dónde. Es lo que te va a evitar redescubrirlo la próxima vez.