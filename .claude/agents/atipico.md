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

1. Antes de despachar nada, **mirá el hit-rate de la caché**. Si da 0 hits, el prompt
   de extracción cambió con una versión nueva de graphify y se re-extrae el corpus
   entero, no solo lo que tocaste: decí el costo antes de arrancar, no después.
2. Corré por la skill `/graphify`, nunca `graphify update <path>` del binario suelto
   —ese es el modo estructural sin LLM, da nodos superficiales y los escribe en
   `specs/graphify-out/` en vez del canónico— y siempre desde la raíz del repo.
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

- La base es Neon. La cadena de conexión vive en user secrets, nunca en el repo.
- La app se conecta como `app_restaurante`, sin `GRANT DELETE`: todo `DELETE`
  falla por diseño. Se anula, no se borra.
- Las migraciones numeradas aplicadas no se editan: los cambios van en una nueva.
- No edites `.claude/settings.json`. Si ves algo mal ahí, reportalo.

## Tu memoria

Escribí en tu memoria lo que descubras y no esté en `CLAUDE.md` ni en los specs:
trampas del entorno, dónde vive cada cosa, decisiones que costó reconstruir.
Notas cortas, con el dónde. Es lo que te va a evitar redescubrirlo la próxima vez.