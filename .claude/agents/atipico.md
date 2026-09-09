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