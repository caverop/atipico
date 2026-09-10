---
name: atipico-grafo-haiku-background
description: "La extracción de graphify va a Haiku en segundo plano; la orquestación (merge, rebuild, poda, commit) la sigue haciendo el agente principal, en vivo, con los guardas de siempre."
metadata:
  type: feedback
---

Cuando toca actualizar el grafo de `specs/` ([[atipico-grafo-con-el-commit]]), el
proceso se parte en dos, y solo una mitad va a segundo plano:

- **Extracción → subagente Haiku, `run_in_background: true`.** Es el paso caro en
  tiempo (3–16 minutos cada corrida el 2026-09-10) y el que menos necesita presencia en
  vivo, porque los guardas de abajo lo atajan igual sin que nadie mire.
- **Orquestación → la sigue haciendo el agente principal, en el hilo, no en segundo
  plano.** Chequear deriva, mergear, rebuildear, verificar contra respaldo, podar
  duplicados, commitear: son llamadas Bash/Python directas, no pasan por el `Agent`
  tool, y es justo donde vive el control que evitó los dos incidentes de abajo.

**Why:** pedido explícito el 2026-09-10 — *"las actualizaciones de los grafos
realizalas en segundo plano con el modelo Haiku"*. Antes de aceptarlo, valía advertir
que la extracción ya falló dos veces esta misma sesión **con un modelo más capaz que
Haiku**: una vez se quedó corta (30 nodos donde había 39, por un prompt centrado en "qué
cambió" que el subagente tomó como el alcance — casi borró tres nodos reales de
decisión), y otra dejó nodos huérfanos por decir el mismo concepto con una palabra
distinta ("Enlaces markdown relativos" vs "Los enlaces son markdown relativo"). Las dos
las agarraron los guardas, no la calidad del modelo. Con Haiku el riesgo esperado de
extracción pobre es mayor, no menor — la respuesta no es evitarlo, es que los guardas
sigan siendo obligatorios.

**How to apply — los guardas no son opcionales, corra quien corra la extracción:**

1. **Piso explícito de nodos** en el prompt del subagente, basado en la corrida anterior
   del mismo archivo (si tenía 75 nodos, pedir un piso de 70, no aceptar menos).
2. **Lista de ids existentes a reusar verbatim**, sacada de `graph.json` antes de
   despachar — sin esto, cada re-frase mintea un id nuevo y dejan huérfanos.
3. **Respaldo de `graph.json` antes del merge** (`cp graph.json .graphify_old.json`) y
   **comparación de ids perdidos/nuevos contra ese respaldo antes de aceptar** — es lo
   único que atrapó los dos incidentes de arriba, y es lo que hace que da igual qué
   modelo extraiga: si perdió algo, se ve antes de escribir `graph.json`.
4. Después del merge, **buscar duplicados por prefijo de etiqueta** (no por similitud de
   id, que da muchos falsos positivos) y podar los reales.

Relacionado: [[atipico-grafo-con-el-commit]], [[atipico-graphify-y-obsidian]].
