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
5. **Spot-check de contenido nuevo explícito — el piso de nodos no alcanza.** Verificado
   el 2026-09-10: Haiku cumplió el piso exacto (45 = 45, cero de más) y **no agregó
   ninguno** de los conceptos que el prompt marcaba como genuinamente nuevos —el bug del
   punto de montaje de `postgres:18-alpine`, encontrado esa misma sesión—. Peor: reusó la
   etiqueta de un nodo existente **sin actualizarla**, dejando en el grafo un dato
   objetivamente falso ("5 propuestos" cuando el archivo real ya decía "4"). El piso de
   nodos protege contra *perder* contenido, no contra *no agregar* el nuevo ni contra
   *dejar viejo* lo que cambió. Después de cada extracción, buscar en el grafo los
   conceptos que el prompt pedía como nuevos por nombre/palabra clave; si no aparecen,
   parchear a mano es más barato y más confiable que otra ronda de subagente —fue lo que
   se hizo, agregando 4 nodos + 6 aristas + 1 corrección de label directo en `graph.json`,
   resincronizando `.graphify_extract.json` después (si no, el guard de #479 rechaza
   escribir por "achicar", porque `rebuild.py` lee de `.graphify_extract.json`, no de
   `graph.json`, y ese archivo se queda desactualizado si se edita el `graph.json` a
   mano).

6. **Typo de ruta, verificado en dos corridas seguidas.** Haiku escribe `pdiego` en vez de
   `pdieg` al copiar la ruta absoluta del `source_file` — no una vez, dos, la segunda peor
   (94 de 98 nodos con la ruta mal escrita, un archivo entero bajo la ruta incorrecta).
   Revisar `Counter(n['source_file'] for n in chunk['nodes'])` **siempre**, antes de mirar
   cualquier otra cosa del chunk; el script de corrección ya existe
   (`fix_typo.py` en el scratchpad de esta sesión, reusable).
7. **"No hagas X" no garantiza que no lo haga — ni repitiéndolo dos veces en el mismo
   prompt.** El prompt de README.md decía explícito, dos veces, "no crear un nodo por fila
   de la tabla" — Haiku creó 25 de todas formas (`readme_spec_<nombre>`), uno por cada
   spec listado, duplicando estructuralmente nodos que cada spec ya tiene desde su propia
   extracción. No fue omisión de contenido nuevo (guarda 5 original) — fue una
   instrucción explícita, negativa, repetida, ignorada. Al podarlos, **cuidado con no
   llevarse de encuentro un id legítimo de una corrida anterior** que coincida con el
   mismo patrón textual (pasó: `readme_spec_postgres_local_dev` sí era real, de una
   extracción previa, y el filtro por prefijo `readme_spec_` lo podó también — hubo que
   recuperarlo de `graph.json` antes del merge). **Chequeo concreto**: contar cuántos
   nodos comparten un mismo patrón de id sospechoso (`grep`/list-comprehension sobre
   `n['id']`) antes de aceptar el chunk, no solo el total.

8. **El typo de `pdiego` es sistemático, no un accidente — van tres corridas seguidas.**
   Una vez, en su propio resumen final, Haiku afirmó *"source paths verified with
   correct 'pdiego' username"* — confirmando la ruta mala como si fuera la buena.
   No confiar nunca en el resumen del subagente sobre esto: `Counter(source_file)`
   sobre el chunk es la única fuente de verdad.
9. **Perder un nodo de la lista de reuso no siempre avisa con un id nuevo — a veces
   simplemente lo omite.** Distinto del incidente de `readme_spec_postgres_local_dev`
   (ahí un filtro de poda se lo llevó de encuentro): acá el subagente tenía 4 ids en su
   lista de reuso explícita y **tres los cumplió, uno se salteó**, sin previo aviso ni
   nodo de reemplazo. El chequeo de perdidos/nuevos contra el respaldo (guarda 3) es lo
   único que lo atrapa — el piso de nodos totales no, porque el chunk igual estaba por
   encima del piso.
10. **`build_merge` no escribe `graph.json` en disco — solo `to_json()` (dentro de
    `rebuild.py`) lo hace.** Si hay que restaurar un nodo perdido después del merge,
    restaurarlo sobre **`.graphify_extract.json`** (lo que `build_merge` acaba de
    producir en memoria), nunca sobre `graph.json` — ese todavía tiene el estado *antes*
    del merge actual, y escribirle ahí pisa el trabajo recién hecho sin que se note hasta
    el rebuild. Pasó una vez: la restauración pareció andar ("0 agregados, 0 aristas") y
    en realidad estaba operando sobre el archivo viejo.
11. Al escribir archivos temporales para restaurar nodos entre pasos, **especificar
    `encoding='utf-8'` explícito tanto al escribir como al leer** — sin esto, en Windows
    el default puede no ser UTF-8 y un archivo con acentos revienta con
    `UnicodeDecodeError` al releerlo.

**Prueba pendiente, pedida el 2026-09-10 tras el tercer typo seguido:** la próxima
extracción real corre con `model: "sonnet"` en vez de `"haiku"`, para comparar
directamente cuánta intervención manual ahorra. No es un cambio de regla todavía —
Haiku sigue siendo el default hasta que se compare esta corrida. Si Sonnet sale limpio
(sin el typo de ruta, sin omitir contenido de la lista de reuso), es el dato que faltaba
para decidir si el ahorro de tiempo en background vale la pena con Haiku o si conviene
mover el default a Sonnet.

Relacionado: [[atipico-grafo-con-el-commit]], [[atipico-graphify-y-obsidian]].
