---
name: atipico-jira-corto-spec-largo
description: El ticket de Jira va corto (lo esencial + puntero al spec); el detalle vive en specs/<feature>.md.
metadata:
  type: feedback
---

Los tickets de Jira se escriben **cortos**: lo esencial accionable, la lista de pendientes,
y un puntero a `specs/<feature>.md`. Toda la extensión —alcance completo, decisiones
descartadas, bitácora de verificación— va en el spec, no en el ticket.

**Why:** el 2026-09-09, en SCRUM-19, escribí primero una descripción larga que duplicaba
casi entero el spec `specs/agente-db.md`. El usuario pidió reducirla: *"quisiera que la
mayor descripcion este en el spec"*. El spec es el registro de la tarea (ver
[[atipico-spec-primero]]); el ticket es la entrada al spec, no una copia suya.

**How to apply:** al escribir o actualizar un ticket, incluir como mucho: qué se hace en
una línea, el link al spec en negrita, 4-6 viñetas de lo esencial (las decisiones que
alguien necesita saber sin abrir el repo), y los pendientes como checklist. Un hallazgo
que exige decisión del usuario sí va al ticket como comentario propio, resumido, porque
tiene que verse sin abrir el repo — pero también resumido, con el detalle en el spec.

**Formato:** el campo acepta `contentFormat: "markdown"`. Nada de sintaxis wiki (`h3.`,
`*negrita*`): queda literal. Usar `###` y `**negrita**`. Los `- [ ]` se renderizan como
`\[ \]` escapado, es cosmético y aceptable.
