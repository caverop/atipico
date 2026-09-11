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

## El grafo se actualiza con el commit

**Si commiteás un cambio en `specs/`, actualizá el grafo en el mismo turno.** No lo
dejes para después: es el único momento en que hay un agente en la conversación, y la
extracción semántica de graphify la hacés vos.

**Salvo que el cambio no le enseñe nada al grafo.** Una corrida cuesta ~90-110k tokens
por archivo; un retoque de redacción no los vale. Se difiere solo si podés **señalar
dónde el grafo ya tiene el dato**: una errata, un reacomodo de formato, o un hecho que
otro spec ya aportó —como la fila del índice que repite un número de migración que el
propio spec de esa feature ya declaró—. Si no podés nombrar la fuente que ya lo cubre,
no es redundante: corré la extracción.

Cuando difieras: **decilo en el mismo mensaje**, porque `detect_incremental` va a
seguir reportando ese archivo hasta que alguien lo extraiga, y una deriva sin explicar
envenena el chequeo. La deuda se salda sola con el próximo cambio de fondo a ese
archivo. Dos topes: no difieras dos veces el mismo archivo, y no acumules más de un
par de archivos diferidos — a partir de ahí corré, aunque cada cambio suelto parezca
menor.

1. Antes de despachar nada, **mirá el hit-rate de la caché**. Si da 0 hits, el prompt
   de extracción cambió con una versión nueva de graphify y se re-extrae el corpus
   entero, no solo lo que tocaste: decí el costo antes de arrancar, no después.
2. Corré por la skill `/graphify`, nunca `graphify update <path>` del binario suelto
   —ese es el modo estructural sin LLM, da nodos superficiales y los escribe en
   `specs/graphify-out/` en vez del canónico— y siempre desde la raíz del repo.

   **La extracción va a un subagente Haiku, en segundo plano** (`Agent` con
   `model: "haiku"`, `run_in_background: true`) — es el paso caro en tiempo (3–16
   minutos por corrida) y el que menos necesita que lo mires en vivo. **La
   orquestación no** — mergear, rebuildear, verificar contra respaldo, podar
   duplicados y commitear los seguís haciendo vos mismo, en el hilo principal, con
   Bash/Python directo. Ahí vive el control, y no es delegable.

   Sea cual sea el modelo que extraiga, estos cuatro guardas son obligatorios, no
   opcionales — atraparon dos incidentes reales el 2026-09-10 con un modelo más
   capaz que Haiku (una extracción que se quedó corta por un prompt mal enfocado, y
   nodos huérfanos por re-frasear el mismo concepto):
   1. Piso explícito de nodos en el prompt, basado en la corrida anterior del mismo
      archivo.
   2. Lista de ids existentes a reusar verbatim, sacada de `graph.json` antes de
      despachar.
   3. Respaldo de `graph.json` (`cp graph.json .graphify_old.json`) y comparación de
      ids perdidos/nuevos contra ese respaldo **antes** de aceptar el merge.
   4. Buscar duplicados por prefijo de etiqueta (no por similitud de id) después del
      merge, y podar los reales.
   5. **Spot-check de contenido nuevo explícito — el piso de nodos no alcanza.** Buscar
      en el grafo, por nombre o palabra clave, los conceptos que el prompt marcaba como
      genuinamente nuevos. Verificado el 2026-09-10: Haiku cumplió el piso exacto y aun
      así no agregó ninguno, y encima dejó una etiqueta reusada con un dato ya falso
      ("5 propuestos" cuando el archivo decía "4"). Si falta, parchear `graph.json` a
      mano es más barato que otra ronda — y después **resincronizar
      `.graphify_extract.json`** con el `graph.json` parcheado, porque el rebuild lee de
      ahí, no del `graph.json`, y si quedan desincronizados el guard de #479 rechaza
      escribir por "achicar".
   6. **Typo de ruta y "no hagas X" ignorado — chequeá siempre, no solo cuando algo se
      ve raro.** Verificado dos veces el 2026-09-10: Haiku escribe `pdiego` en vez de
      `pdieg` en el `source_file` (corregible con el script `fix_typo.py`), y una vez
      creó 25 nodos por fila de tabla pese a que el prompt lo prohibía explícito, dos
      veces. Antes de aceptar un chunk: `Counter(source_file)` para el typo de ruta, y
      contar cuántos ids comparten un patrón sospechoso (`grep`/list-comprehension) para
      lo segundo. Si podás algo, cuidado con llevarte de encuentro un id legítimo de una
      corrida anterior que matchee el mismo patrón — recuperalo de `graph.json` antes.
3. **Al empezar cualquier trabajo sobre specs, chequeá deriva primero.** El usuario
   commitea desde su terminal y ahí no hay agente, así que la regla no se disparó:

   ```bash
   $(cat graphify-out/.graphify_python) -c "from graphify.detect import detect_incremental; from pathlib import Path; r=detect_incremental(Path('specs')); print('cambiados:', r.get('new_total',0), '| borrados:', len(r.get('deleted_files',[])))"
   ```

   Es la respuesta autoritativa: detecta contenido cambiado, no solo archivos nuevos.
   Si hay deriva, decilo antes de empezar.

**No hay hook, y no hay que volver a proponerlo.** Ni de git —corre sin agente, no
tiene con qué hacer la parte semántica— ni de Claude Code, que viven en
`settings.json` y no se tocan. Pero la razón de fondo es más simple: un hook solo
podría *avisar*, nunca actualizar, y el aviso ya lo da el chequeo de arriba. Además
**el grafo tiene un solo lector: vos, el agente.** El usuario confirmó el 2026-09-09
que nunca abre `graph.html` por su cuenta. Así que el grafo solo necesita estar al día
en el momento en que lo vas a usar, y ese es exactamente el momento en que chequeás.
La ventana de desactualización cae donde nadie mira.

**Por qué importa:** el grafo llegó a atrasarse 15 documentos sin que nada avisara, y
lo que destapó al ponerlo al día fue una colisión de numeración de migraciones contra
una que ya estaba en producción — anotada en un spec días antes como pendiente y nunca
releída. El grafo es lo que vuelve a poner sobre la mesa lo que quedó escrito donde
nadie mira.

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
- **Bajo ningún concepto te conectás a Neon, salvo pedido explícito del usuario en ese
  momento.** No de oficio, no "para verificar", no porque una tarea parezca requerirlo.
  `qa` y `production` los corre el usuario siempre a mano — él te pasa el resultado y
  vos lo verificás sobre eso, no conectándote vos. Si algo parece necesitar tocar Neon
  (por ejemplo, regenerar `sql/schema_completo.sql` con `pg_dump`), entregale el comando
  exacto y esperá su resultado — no lo corras vos, ni siquiera de lectura, a menos que
  él te lo pida en el momento.
- La app se conecta como `app_restaurante`, sin `GRANT DELETE`: todo `DELETE`
  falla por diseño. Se anula, no se borra. Es así en Neon y en `localhost:5433` —
  `docker-compose.db.yml` corre el mismo `script_inicial.sql` con el mismo `BLOQUE 2`.
- Las migraciones numeradas aplicadas no se editan: los cambios van en una nueva.
- No edites `.claude/settings.json`. Si ves algo mal ahí, reportalo.

## Tu memoria

Escribí en tu memoria lo que descubras y no esté en `CLAUDE.md` ni en los specs:
trampas del entorno, dónde vive cada cosa, decisiones que costó reconstruir.
Notas cortas, con el dónde. Es lo que te va a evitar redescubrirlo la próxima vez.