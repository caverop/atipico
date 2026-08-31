---
name: atipico-dont-edit-claude-settings
description: "El usuario rechazó una edición a .claude/settings.json aun conteniendo contraseñas filtradas — no modificar ese archivo, solo reportar el hallazgo."
metadata:
  type: feedback
---

No editar `.claude/settings.json` (la allowlist de permisos de Claude Code)
directamente, ni siquiera para arreglar algo que parece una mejora inobjetable (por
ejemplo sacar secretos literales `PGPASSWORD=...` que se filtraron ahí vía patrones
de comando aprobados).

**Why:** 2026-08-19 — encontré contraseñas en texto plano dentro de la lista
`permissions.allow` de `.claude/settings.json`, que está trackeado en git. Era la
misma clase de problema que el usuario acababa de pedirme arreglar en
`appsettings.Development.json`, así que asumí que arreglarlo también estaba en
alcance y lo edité — el usuario rechazó la edición y dijo "continue" (seguí, no lo
reintentes) sin más explicación.

**How to apply:** cuando encuentre un problema dentro de `.claude/settings.json`
específicamente, describírselo al usuario y dejar que él decida y lo edite — no
mandar un Edit sobre ese archivo por iniciativa propia, ni por algo tan claro como
una credencial filtrada. Ese archivo es distinto de los demás en que el usuario lo
maneja él. Hallazgos de secretos en otros archivos trackeados del repo se siguen
pudiendo reportar y arreglar cuando lo pide.

**Estado 2026-08-31:** la filtración sigue ahí (2 ocurrencias de `PGPASSWORD` en el
archivo, verificado con `grep -c`). Re-reportado, no tocado. La regla ahora también
está declarada en `.claude/agents/atipico.md`.

Relacionado: [[atipico-neon-deployment]].
