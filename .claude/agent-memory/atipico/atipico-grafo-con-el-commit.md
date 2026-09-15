---
name: atipico-grafo-con-el-commit
description: "El agente ya NO actualiza el grafo al commitear specs/ — el usuario lo revirtió el 2026-09-14 y lo hace manualmente él mismo. No despaches la extracción de oficio."
metadata:
  type: feedback
---

**Revertido el 2026-09-14.** Desde el 2026-09-09 hasta esa fecha, la regla fue: al
commitear un cambio en `specs/`, actualizar el grafo de graphify en el mismo turno. El
usuario la sacó explícitamente — *"borrar la instrucción que al commitear actualices el
grafo, esto lo haré yo manualmente desde ahora"* — en la misma sesión en que también se
descartó la automatización completa (`tools/grafo-actualizar.py`, ver
`specs/actualizacion-grafo-script.md`, `descartado`).

**Why:** dos decisiones el mismo día apuntan en la misma dirección — el usuario quiere
menos intervención del agente sobre el grafo, no más. Primero rechazó mecanizar la
orquestación en un script; después sacó directamente la instrucción que disparaba la
extracción manual-pero-automática en cada commit.

**How to apply:** no corras la extracción de graphify como parte de commitear specs, ni
la ofrezcas de oficio. El grafo lo mantiene el usuario a mano. Si él mismo pide ayuda
puntual con una corrida, la receta con guardas (prompt rico, piso de nodos, ids a
reusar, verificación contra respaldo) sigue documentada en
[[atipico-grafo-gemini-background]] — pero la decisión de cuándo correrla es de él, no
una regla de este agente.

La regla vive (ausente, a propósito) en `.claude/agents/atipico.md`, sección "El grafo lo
mantiene el usuario". Relacionado: [[atipico-spec-primero]], [[atipico-graphify-y-obsidian]].
