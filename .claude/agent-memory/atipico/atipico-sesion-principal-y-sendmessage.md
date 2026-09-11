---
name: atipico-sesion-principal-y-sendmessage
description: "SendMessage está deshabilitado en las sesiones de atipico; usar los tools mcp__ccd_session_mgmt__* (list_sessions, get_session, archive_session) para gestionar sesiones hermanas. Esta sesión quedó marcada como la principal el 2026-09-10."
metadata:
  type: reference
---

**`SendMessage` (para hablar con sesiones hermanas vía `ListAgents`) está deshabilitado**
en esta sesión y en sus subagentes — probado el 2026-09-10, tira `No such tool
available`. No sirve para comparar contexto con otra sesión ni para pedirle nada en vivo.

**Lo que sí funciona:** `mcp__ccd_session_mgmt__list_sessions` (lista con `cwd`,
`título`, `lastActivityAt`, `isRunning`), `get_session` (metadata puntual) y
`archive_session` (cierra una sesión — recuperable desde Archivadas, no borra nada). Para
identificar cuáles corresponden a los pares que reporta `ListAgents` (que da nombres tipo
`atipico-83 [4a4c7b]`, sin relación textual obvia con el `sessionId` de
`ccd_session_mgmt`), cruzar por `cwd` + actividad reciente + tema, no por el id.

**El 2026-09-10 se cerraron** "Database agent" (`local_a4077d35-...`) y "Subagente DB
para base de datos" (`local_62d8d9dd-...`), las dos del `2026-09-09` en este mismo repo,
a pedido del usuario — quería mantener *esta* sesión (atipico-9a) como la principal por
ahora. Si en una sesión futura el usuario pregunta por "la otra sesión" o pide comparar
con "la principal", primero preguntar cuál es — puede haber cambiado.
