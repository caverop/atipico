---
name: auditar-specs
description: Inicia la auditoría de specs/ en sus tres dimensiones — estado declarado contra el índice (con auditar-specs.ps1, modelo local de Ollama, solo lectura), conformidad con el formato obligatorio de formato-spec.md, y hallazgos de contenido. Reporta con evidencia; no reescribe specs.
whenToUse: El usuario pide auditar, revisar o pasar revista a los specs, o pregunta si el índice y los specs coinciden.
---

# Auditar los specs

**Esto reporta; no arregla.** `specs/formato-spec.md` §10.1 lo fija: los specs viejos no se
reescriben en masa — adoptan el formato *cuando se los toca por otra razón*. Cualquier
corrección que salga de la auditoría pasa por el orden del proyecto (spec → plan →
aprobación → código) y por la decisión del usuario. Nunca edites specs "de paso" porque la
auditoría los marcó.

## Dimensión 1 — estado declarado vs índice

Es la única que necesita un modelo, porque el estado en los specs viejos es prosa
(`**Estado:** implementado`, `- **Estado:** **propuesto**`…). Herramienta versionada en la
raíz: `auditar-specs.ps1`, contra **Ollama local**, sin red externa ni base de datos.

```powershell
cd C:\Users\pdieg\atipico
& .\auditar-specs.ps1                        # llama3.2:3b por defecto
& .\auditar-specs.ps1 -Modelo gemma3:4b      # contraste
& .\auditar-specs.ps1 -Semilla 7             # veredictos estables vs azar del muestreo
```

**Cómo leer la salida, sin creerle de más:**

- Es un **filtro, no un veredicto**: por cada caso trae la línea cruda `Evidencia:` para que
  el humano confirme. Los modelos de 3-4B aciertan alrededor de la mitad de los casos
  ambiguos (está medido en la cabecera del script).
- `?` / `REVISAR` = el modelo no supo, no = el spec está mal.
- `DISCREPA` = el estado del spec y el del índice no coinciden: **confirmalo leyendo la
  línea**, y recién entonces preguntá cuál de los dos está bien.
- El veredicto `SIN-FILA` es el único mecánico: el spec no está en el índice.
- Corre ~10-20 s por spec en CPU (sin GPU): 26 specs son varios minutos → **en segundo
  plano**, y no lo relances mientras corre.
- Desde 2026-09-14 hay además specs con `estado:` en el frontmatter (el dato, según
  `formato-spec.md` §4.1): para esos la comparación es exacta y no hace falta modelo. Si la
  mayoría tuviera frontmatter, esta dimensión se vuelve una regex y el modelo sobra.
- **Pero compará las DOS copias del estado, no sólo el frontmatter.** El formato deja la
  línea de prosa en la cabecera como redundancia *a propósito* (§4.2), así que las dos pueden
  divergir: el 2026-09-14 la auditoría encontró a `formato-spec.md` con
  `estado: implementado` arriba y `**Estado:** **propuesto, pendiente de aprobación**` en el
  cuerpo — en el documento que define el formato. Un spec cuyo frontmatter coincide con el
  índice puede tener igual la prosa vencida, y el modelo que lee el cuerpo va a reportar
  `DISCREPA` con razón. Antes de culpar al modelo, mirá si el spec se contradice a sí mismo.

## Dimensión 2 — formato obligatorio (mecánica, sin modelo)

Contra la tabla de `formato-spec.md` §4.3. Chequeá **por nombre de sección, no por número**:
la mayoría de los specs existentes no numera (`## Silogismo` en vez de `## 2. Silogismo`), y
buscar el número da falsos incumplimientos.

| Qué | Cómo se ve |
|---|---|
| Frontmatter con `estado` ∈ {propuesto, aprobado, implementado, en-produccion, descartado} | al tope del archivo |
| `**Hoy:**` | resumen del estado actual |
| `**Contenido:**` | obligatorio si pasa de 8 secciones |
| Silogismo **con cláusula de reapertura** | "…esta decisión se reabre"; sin ella el silogismo es una opinión con formato |
| Decisión con **≥1 diagrama Mermaid** | bloque ```mermaid (secuencia para flujos, estado para máquinas de estado) |
| Tabla de **Artefactos** con rutas reales | no nombres de proyecto |
| Criterios de aceptación | verificables, no aspiracionales |
| Plan de implementación | numerado |
| Bitácora | descartados · errores · verificado |
| `**En pocas palabras:**` | el cierre |
| Enlaces entre specs | markdown relativo, **nunca** `[[wikilinks]]`; que resuelvan a un archivo que existe |
| Índice | una fila por spec, con el estado que declara el propio spec |

## Dimensión 3 — contenido (juicio, con evidencia)

Acá no hay herramienta: se lee y se reporta. Lo que vale la pena buscar:

- **Afirmaciones vencidas**: un spec que describe algo que ya cambió (una migración, un
  entorno, una decisión posterior). El grafo ayuda: comunidades y nodos compartidos hacen
  visible qué specs hablan del mismo tema.
- **Contradicciones entre specs** sobre el mismo hecho.
- **Estado que no refleja la realidad**: dice `implementado` y falta correr el script (el
  proyecto distingue `implementado` de `en-produccion` justo por esto).
- **Superseded sin marcar**: un spec reemplazado por otro sin decirlo en su cabecera.

Cada hallazgo va con la cita y el archivo. Nada se corrige en el momento.

## Salida de la auditoría

Una tabla de hallazgos — spec, dimensión, qué se encontró, evidencia (línea o cita), y si es
mecánico (no admite discusión) o de criterio (admite) — más el resumen por dimensión. Después
**preguntá qué se arregla y en qué orden**; los arreglos que toquen código o specs van por el
orden de siempre, y el usuario decide.
