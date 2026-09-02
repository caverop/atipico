---
name: atipico-spec-primero
description: "Flujo de trabajo en Atipico — spec, luego plan, luego aprobación, luego código; ante un cambio, se actualiza primero el spec y después el plan."
metadata:
  type: feedback
---

El usuario trabaja Atipico con este orden y lo hace cumplir, sin que haga falta
pedirlo: **spec → plan → aprobación → código**. Cuando pide un cambio sobre algo ya
implementado, la corrección va igual en ese orden: *"primero modifica el spec luego
el plan"*. Lo ha pedido repetidamente: *"realiza el plan y diagramas previamente"*,
*"primeramente actualiza el spec"*, *"primero el spec y luego planifica los
cambios"*, *"revisa el spec y dame tu punto final antes de implementarlo"*.

**Formato del documento.** Cada feature tiene el suyo en `specs/<feature>.md`, en
español, con secciones numeradas y cabecera de estado / alcance / fuera-de-alcance.
Incluye el razonamiento detrás de cada decisión, el plan de implementación numerado
como sección propia, una sección de despliegue a producción, y diagramas Mermaid
cuando el flujo lo pide. La línea de estado se actualiza cuando la feature sale.

El spec no es solo diseño ni una propuesta que se tira después: lleva una bitácora
con los diseños descartados, los errores corregidos y lo verificado empíricamente.
Es el registro de la tarea.

**Why:** los specs son el registro duradero del *porqué*, para que las decisiones no
se vuelvan a discutir meses después. El usuario los revisa antes de comprometerse al
código y ha cambiado de dirección en fase de spec más de una vez — mucho más barato
que hacerlo con el código escrito. Además ya pasó dos veces en la feature de turnos (`specs/numero-pedido.md`)
que el código se escribió antes de que el spec lo registrara, y el usuario lo marcó
explícitamente la segunda.

**How to apply:** ante una feature nueva, no empezar editando código — escribir el
spec primero y decirlo. Ante un pedido de cambio sobre una feature con spec,
actualizar `specs/<feature>.md` (diseño + reglas de negocio + criterios de
aceptación) y después la sección del plan, antes de tocar código. Al revisar una
decisión anterior, actualizar la sección afectada del spec en el mismo turno que el
cambio de código. Si el código ya se escribió, decirlo plano: el spec queda como
registro de lo hecho, no como propuesta previa.

Este es el mismo orden que ya declara `.claude/agents/atipico.md`; la memoria agrega
el formato del documento y las frases textuales con que el usuario lo pide.

Los specs viven en `specs/` (renombrada desde `docs/` el 2026-08-31).

Relacionado: [[atipico-improvement-plan]], [[atipico-user-tests-himself]].
