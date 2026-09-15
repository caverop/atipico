---
name: actualizar-grafo
description: Ejecuta una actualización del grafo de specs/ contra la API de Gemini, cuando el usuario lo pide explícitamente. Arma el prompt rico por archivo, aplica los guardas, verifica contra el respaldo, mergea, etiqueta las comunidades y reporta. No se corre por iniciativa propia ni como parte de commitear.
whenToUse: El usuario pide explícitamente actualizar, poner al día o reconstruir el grafo de specs/.
---

# Actualizar el grafo de `specs/` (pedido explícito)

**Regla de oro: esto corre solo si el usuario lo pide.** Desde el 2026-09-14 el grafo lo
mantiene él, a mano, cuando quiere — `.claude/agents/atipico.md` lo dice literal: *"No
corras la extracción de graphify por tu cuenta… No lo despaches como parte de commitear,
ni lo ofrezcas de oficio."* Si al commitear `specs/` te parece que el grafo quedó atrás,
**decilo una vez** y esperá: no lo corras ni lo ofrezcas como paso del commit.

La receta de abajo es la misma que documenta
`.claude/agent-memory/atipico/atipico-grafo-gemini-background.md`, que es la **fuente de
verdad**: ahí están los guardas con la evidencia que los justifica. Esta skill es el cómo;
si algo difiere, manda la memoria.

- **Corpus:** `specs/` (la raíz de escaneo; el grafo vive en `graphify-out/` en la raíz del
  repo). Nunca `graphify update <path>` del binario suelto: es el modo estructural sin LLM,
  da nodos superficiales y los escribe en `specs/graphify-out/` en vez del canónico.
- **Modelo:** Gemini por API. No hay subagentes en este camino.
- **Si no hay grafo previo** (se borró `graphify-out/`, o el usuario pide "generar todo de
  nuevo"), esto no es una actualización sino una **reconstrucción en frío**: andá a §8. Los
  pasos de §1 a §5 suponen un grafo del que sacar pisos y ids.

## 0. Entorno (si algo falla, es casi siempre acá)

```powershell
$env:GEMINI_API_KEY = [Environment]::GetEnvironmentVariable('GEMINI_API_KEY','User')  # DSH no hereda lo nuevo
$env:GRAPHIFY_API_TIMEOUT = '150'    # el default es 600s: un modelo muerto cuelga 10 minutos
$env:PYTHONIOENCODING = 'utf-8'      # PYTHONUTF8=1 NO alcanza: cp1252 revienta imprimiendo etiquetas
$py = Get-Content graphify-out/.graphify_python   # el venv de uv, que es el que tiene el paquete
# Si graphify-out/ no existe todavia (reconstruccion en frio), ese archivo no esta: usar
# C:\Users\pdieg\AppData\Roaming\uv\tools\graphifyy\Scripts\python.exe
```

- El backend `gemini` va por el endpoint compatible con OpenAI: necesita `openai` y
  `tiktoken` en ese venv. Si faltan (`ModuleNotFoundError: No module named 'openai'`):
  `& $py -m ensurepip --upgrade` y después `& $py -m pip install --no-cache-dir openai tiktoken`.
  Ojo: escribir ahí es **fuera del workspace** (pide aprobación de sandbox), y el venv de uv
  no trae pip. Si algún día se reinstala uv: `uv tool install "graphifyy[gemini]" --force`.
- **Cuota:** es del free tier y **por modelo**. Se agota rápido (~15-20 llamadas por modelo
  y por día). Un 429 no es un defecto del grafo: es cupo. Rotá de modelo.

## 1. Qué cambió (la deriva), antes de extraer nada

```python
from graphify.detect import detect_incremental
d = detect_incremental(Path('specs'))
d['new_files']   # el SUBCONJUNTO cambiado, por tipo  <-- el que importa
d['new_total']   # cuántos
d['files']       # el corpus COMPLETO por tipo (27 docs)
d['deleted_files']
```

`files` es el corpus entero, no lo tocado: confundirlos es lo que borró el `semantic_hash`
de 21 archivos intactos el 2026-09-14. **Decile al usuario qué va a extraer y cuánto antes
de arrancar**; si el cambio no le enseña nada al grafo, mencionalo una vez y dejá que decida.

## 2. El prompt rico, validado (sin fallback)

El prompt vive en el bundle de la skill `graphify`:
`references/extraction-spec.md`. Ubicalo (`.dsh/skills/graphify`, `.agents/skills/graphify`,
`~/.claude/skills/graphify`) y **validalo**: ≥5000 caracteres y que contenga `Node ID format`.
Si no aparece, **no corras**: el prompt corto (`_EXTRACTION_SYSTEM`, 3301 chars) devolvió 11
nodos donde el grafo tenía 53.

Del bloque de código del spec: sustituí `TOTAL_CHUNKS` y `CHUNK_NUM` por `1` (en ese orden),
`FILE_LIST` por el nombre del archivo, y borrá la instrucción de escribir el chunk a disco
(la ruta API devuelve el JSON). Después agregá los dos guardas que cierran la brecha:

```
COVERAGE FLOOR: la extraccion previa verificada de este documento dio N nodos. Si emits
menos de N-5 sub-extraiste: relee el documento antes de responder. Cada concepto, entidad,
mecanismo, regla, trade-off o decision con nombre propio merece su nodo.

REUSE THESE EXISTING NODE IDS VERBATIM: <los ids del archivo, sacados de graph.json>
```

Para un archivo **nuevo** no hay N medido: estimá por densidad (mediana de nodos/KB del
corpus, ~1.1; factor 0.5) y **reportá** si quedó por debajo — no abortes, es una estimación.

Se inyecta con `graphify.llm._EXTRACTION_SYSTEM = prompt` (la función lee la constante en
tiempo de llamada) y se llama **un archivo por llamada**:
`m.extract_files_direct([SPECS/nombre], backend="gemini", model=modelo, root=SPECS)`.

**Y parcheá `graphify.llm._FILE_CHAR_CAP` antes de extraer: vale `20000` por defecto, y
`_read_files` hace `content[:_FILE_CHAR_CAP]`.** O sea que la ruta API **trunca cada archivo a
20.000 caracteres**. Medido el 2026-09-15 sobre `numero-pedido.md` (61.678 bytes): con el cap,
la llamada mandó **7.865** tokens de entrada; con el cap en 500.000, **20.025** — el modelo veía
un tercio del spec. Dos consecuencias que hay que tener presentes:

- Un piso es **imposible de cumplir** si el contenido no llegó. El modelo no puede extraer lo
  que no recibió, y va a responder lo que pueda del fragmento.
- La lista de ids puede volver como **eco** en vez de extracción. Es la lectura correcta del
  "53 nodos con 31 ids coincidentes" del 2026-09-14: con el documento truncado y los 53 ids
  servidos en el prompt, el modelo tenía de dónde copiarlos. Con el cap parcheado y **sin** lista
  de ids, ese archivo dio **26 nodos** reales.

Con el cap parcheado la densidad sube a ~0,43 nodos/KB, que sigue siendo la mitad del histórico
(0,88 en ese archivo). Ese techo es de la ruta API, no del modelo: ver §8.5.

**Modelo:** probá uno con un ping barato y quedate con el que responda. Medidos el
2026-09-14: `gemini-3.6-flash` (2.8 s) y `gemini-3.5-flash` andan; **`gemini-3.8-flash` y
`gemini-flash-latest` se cuelgan**; `gemini-2.5-flash` y `gemini-2.5-flash-lite` dan **404**
aunque `/v1beta/models` los liste. Ante 429, pasá al siguiente modelo en vez de abortar.

## 3. Verificar por archivo, antes de mergear

| Chequeo | Por qué |
|---|---|
| `Counter(source_file)` == el archivo, una sola clave | el typo `pdiego`/`pdieg` de la época de Haiku metía un archivo entero bajo otra ruta |
| ids perdidos contra el respaldo → **abortar** | es lo único que atrapa una extracción corta y la omisión de un id de la lista |
| nodos ≥ piso | sin piso: 11 nodos donde había 53 |
| **aristas viejas vs nuevas, y unión** | la extracción por archivo **no puede** producir las citas entre specs: esos 5 archivos tenían 206 aristas y la extracción devolvió 52. El guard de nodos no lo ve |
| 0 aristas con extremo inexistente | la unión solo es segura si no se perdió ningún id |

## 4. Merge y escritura (el orden importa)

1. **Respaldo primero:** `graph.json` → `.graphify_old.json`, `.graphify_extract.json` → `.bak`.
2. `build_merge([extraccion], graph_path=..., root=SPECS)` — reemplaza por `source_file`; **no
   escribe**.
3. `cluster`, `score_all`, `god_nodes`, `surprising_connections`.
4. **Etiquetas de comunidad ANTES de escribir** (`generate_community_labels(..., backend="gemini")`).
   Escribir sin etiquetas deja `community_name` viejo pegado a un `community_id` nuevo, y
   `graph.json` sigue siendo válido: nada avisa.
5. `to_json(G, comunidades, 'graphify-out/graph.json', community_labels=etiquetas)` —
   **sin `force`**: el guard de achicar (#479) es la última red.
6. Reporte (`GRAPH_REPORT.md`), `.graphify_labels.json`, `.graphify_analysis.json`,
   `.graphify_extract.json` sincronizado (el rebuild lee de ahí, no de `graph.json`).
7. **Manifest con el corpus COMPLETO**: `_stamped_manifest_files(d['files'], extraccion, SPECS)`
   + `save_manifest(..., clear_semantic=(new_files − sellados) or None)`.
8. `cost.json` con el modelo usado.

## 5. Verificación final y reporte

- `detect_incremental` debe dar **`new_total == 0`**.
- Chequeo de salud: 0 aristas colgantes, 0 sin extremo, 0 self-loops.
- **Los dos juicios que reportás y NO decidís** (son los únicos que no se pueden mecanizar):
  nodos marcados `verification=unverified` (graphify los cuenta en stderr) y pares de
  etiquetas donde una es prefijo de la otra dentro del mismo archivo. Se reportan con datos;
  **podar es decisión del usuario** — el 2026-09-10 una poda por prefijo se llevó un id
  legítimo de más.
- Números para saber si la corrida tiene sentido: el corpus son 27 documentos; el 2026-09-14
  el grafo quedó en **617 nodos, 776 aristas, 41 comunidades**, con ~10-15k tokens de entrada
  por archivo y costo **$0** en free tier. Si el total de aristas **baja**, algo salió mal:
  revisá el guard de la §3 antes de dar la corrida por buena.

## 6. El vault de Obsidian (parte del update)

El vault es `specs/`; las notas generadas viven en `specs/grafo/`: una nota por nodo más una
`_COMMUNITY_*.md` por comunidad. **Se actualiza en la misma corrida**, después de que el grafo
quedó escrito y verificado — si el grafo cambió y el vault no, el vault quedó viejo.

```powershell
graphify export obsidian --dir graphify-out/obsidian-nuevo   # a un directorio de descarte
```

El export escribe una nota por nodo con `[[wikilinks]]` —legítimo **acá**: son generadas,
gitignoreadas y nunca llegan a GitHub— más `graph.canvas`. **No se copia todo:**

| Se copia | Por qué |
|---|---|
| `*.md` (nodos y `_COMMUNITY_*`) | son las notas |
| `graph.canvas` | el lienzo |
| **NO** `.obsidian/` | el export trae su propia configuración y pisaría la del vault |
| **NO** `.graphify_obsidian_manifest.json` | es del export, no del vault |

**Podar primero, copiar después.** El vault puede tener notas de nodos que ya no existen, o con
nombres que difieren en un espacio invisible: si copiás y después podás, la poda puede borrar la
nota recién copiada. Pasó el 2026-09-14 —el vault quedó con 1 nota menos que el export y el
nombre culpable era uno que Windows normaliza al mismo archivo—. Orden: calcular qué sobra,
borrarlo, y recién entonces copiar lo nuevo.

**Dos verificaciones, las dos obligatorias:**

1. **El vault tiene que quedar con el mismo número de notas que informa el export.** El comando
   imprime `Obsidian vault: N notes`; contá `specs/grafo/*.md` y comparalo. Si no coincide,
   falta copiar algo (es exactamente lo que atrapó la nota perdida).
2. **`detect_incremental` tiene que seguir en 0.** El corpus es `specs/`, así que las notas
   están *dentro* del corpus: lo único que impide que graphify extraiga un grafo de su propia
   salida es la línea `specs/grafo/` del `.gitignore`, que graphify lee. Si esa línea
   desaparece, la corrida siguiente se come cientos de documentos nuevos. Verificado el
   2026-09-14 con el vault poblado: deriva **0** y corpus en 28 documentos.

Baseline de ese día: 627 nodos + 42 comunidades → **669 notas**, y el vault pasó de 193 a 669
(636 agregadas, 160 podadas, 33 reescritas).

## 7. Qué NO hace esta skill

- No commitea (regla del proyecto: sin aprobación explícita).
- No corre sola ni como parte de un commit (§ regla de oro).
- No regenera los otros exports (`graph.html`, `svg`, `graphml`, `wiki`).

## 8. Reconstrucción en frío (sin grafo previo)

Cuando `graphify-out/` se borró —o el grafo quedó irrecuperable, o el usuario pide "generar todo
de nuevo"— esto **no es una actualización**: no hay pisos, no hay lista de ids, y no hay nada
que proteger. El método es otro, y §1–§5 no aplican tal cual.

### 8.1 La trampa que costó un grafo entero (2026-09-14)

**No uses `graphify extract specs --backend gemini` para reconstruir.** Ese camino usa el prompt
corto (`_EXTRACTION_SYSTEM`, 3301 chars), no el rico. Medido a escala de corpus: **28 documentos
→ 28 nodos**, uno por documento — nodos con el nombre del archivo y el título del spec
(`numero_pedido` / "Número de pedido"), sin un solo concepto de adentro. Termina con **exit 0**
y un reporte que parece sano: nada avisa.

**Firma para reconocerlo:** ~1 nodo por archivo, ids que son el stem del archivo, y notas de
graphify del tipo `kept 'Número de pedido', dropped 'Número de pedido por turno'`. Si ves eso,
el prompt rico no se aplicó: **no lo aceptes**.

Y un detalle del CLI: **`--out` es el directorio PADRE**, no el destino. `--out graphify-out`
escribe `graphify-out/graphify-out/graph.json`.

### 8.2 El método

1. **El prompt rico inyectado**, el mismo de §2, validado, con los pisos adentro.
2. **Lotes de ~6 documentos por llamada** (≈55k tokens, bajo el presupuesto de 60k). El lote es
   lo que hace posibles las citas *entre* specs; por archivo suelto no se generan.
3. **Piso por densidad**, porque no hay nada medido: la densidad histórica del corpus es
   **~1,1 nodos/KB** (627 nodos sobre ~500 KB), y en una reconstrucción se apunta a eso —factor
   **1.0**, no el 0.5 que se usa para un archivo nuevo en una corrida incremental. Ejemplo:
   `numero-pedido.md` tiene 60 KB → piso ~65; su última extracción real dio 53.
4. Una llamada por lote con `extract_files_direct(lote, backend="gemini", model=..., root=SPECS)`,
   y **guardá el resultado de cada lote a disco a medida que termina**
   (`.graphify_gemini_lote_N.json`): si el cupo se corta a mitad, se reanuda sólo lo que falta.
5. Sin grafo previo no hay `build_merge` que splicear: se arma con
   `build_from_json(extraccion, root=SPECS)`. Después `cluster`, `score_all`, `god_nodes`,
   `surprising_connections`.
6. Etiquetas con Gemini, y **recién ahí** `to_json(G, comunidades, 'graphify-out/graph.json',
   community_labels=etiquetas)`.
7. Reporte, `.graphify_labels.json`, `.graphify_analysis.json`, `.graphify_extract.json` (la
   extracción concatenada de todos los lotes), manifest con el corpus completo y `cost.json`.
8. **El vault, al final** (§6). Si el grafo falla, el vault no se toca.

### 8.3 Verificación

- **Densidad por archivo cerca de ~1,1 nodos/KB.** Si sale ~0,03 (1 nodo por archivo), falló el
  prompt: es la firma de §8.1.
- **El total tiene que dar en los cientos, no en las decenas.**
- Baselines medidos el 2026-09-14, para comparar archivo por archivo:
  `reparacion-ck-cuenta-metodo` **83**, `numero-pedido` **53**, `README` **21**, `formato-spec`
  **15**, `deploy-azure-aspire` **13**, `limpieza-datos-prueba` **12**,
  `pruebas-blazor-marca-login` **12**, `enlace-corto-ubicacion` **11**, `auditoria-specs` **10**,
  `actualizacion-grafo-script` **8**.
- Salud sin colgantes ni self-loops, y `detect_incremental` en **0** al terminar.

### 8.4 Costo y cupo

~5-6 llamadas de extracción más 1 de etiquetado. El free tier da ~15-20 llamadas **por modelo y
por día**: alcanza, pero si un 429 corta la corrida, los lotes ya escritos quedan y se reanuda.
Para referencia, la corrida del CLI con el prompt corto gastó **146k tokens de entrada** para el
corpus entero; con el prompt rico sube un poco (el prompt son ~2k tokens por llamada) y sigue
siendo **$0** en free tier.

### 8.5 El techo real de la ruta API, medido el 2026-09-15

La corrida por lotes de ese día **falló su propia verificación** y hay que saber por qué antes de
repetirla:

- **Lotes de 5-6 documentos: 124 nodos en 28 archivos (0,25 nodos/KB), cada archivo bajo su
  piso.** `reparacion-ck` dio 3 donde el histórico es 83; `numero-pedido` 16 donde es 53.
- **Lotes de 2: peor todavía** — 6 nodos para `numero-pedido`.
- **Un archivo por llamada, con el cap parcheado y sin lista de ids: 26 nodos** para ese mismo
  archivo (0,43 nodos/KB).

O sea: **la ruta API no reproduce la densidad del grafo que existía.** Aquel grafo de 627 nodos
lo construyeron **subagentes que leían los archivos ellos mismos** (la época de Haiku: un agente
por lote, con herramientas de lectura, iterando). Un solo request por archivo, con el documento
entero, llega a la mitad. Para una reconstrucción **densa** hay que volver a ese camino:
subagentes (en DSH, la herramienta `subagent` o `workflow`) con el prompt de
`references/extraction-spec.md` y un piso por archivo. La ruta API sirve para lo incremental,
donde el grafo previo ya tiene el contenido y lo que se busca es no perderlo.

**Cupo del free tier, medido:** ~5 llamadas por modelo y por día (`gemini-3.5-flash` cortó a la
quinta, `gemini-3.5-flash-lite` a la sexta). Una reconstrucción de 28 documentos por archivo
**no entra en un día**: son 2-3 días rotando modelos, o un tier pago.

### 8.6 La reconstrucción por subagentes, que es la que funcionó (2026-09-15)

La contracara de §8.5, y la conclusión operativa: **7 subagentes de DSH** (uno por grupo temático
de los que arma el índice de `specs/README.md`, ~4 specs cada uno), cada uno **leyendo sus
archivos enteros**, con el prompt rico y un piso por archivo, y escribiendo su JSON a
`.graphify_agente_lote_N.json`. Resultado:

| | nodos | aristas | n/KB |
|---|---|---|---|
| 7 lotes de subagentes | **1302** | **1635** | 2,75 |
| el grafo anterior (627) | 627 | 805 | ~1,25 |

Comparado archivo por archivo contra los baselines de §8.3: `numero-pedido` 53 → **140**,
`reparacion-ck-cuenta-metodo` 83 → **111**, `deploy-azure-aspire` 13 → **67**, `formato-spec`
15 → **40**, `pruebas-blazor-marca-login` 12 → **32**, `enlace-corto-ubicacion` 11 → **27**,
`README` 21 → **35**, `auditoria-specs` 10 → **24**. Todos entre 1,3× y 5× el histórico.

**Eso confirma que el grafo viejo estaba flaco, no que este esté inflado:** 0 nodos sin
evidencia, 0 nodos aislados, 0 ids inválidos, 0 aristas colgantes, y los tipos repartidos como
corresponde (`code`/`concept`/`rationale`/`document`). El grafo quedó en **1302 nodos, 1618
aristas y 132 comunidades**, y el vault en **1434 notas** (1302 + 132).

**Regla que sale de esto: para reconstruir, subagentes; la ruta API queda para lo incremental.**
Un request suelto no lee un spec de 60 KB —un subagente sí—, y la diferencia es de 2× a 5× en
nodos por archivo. El costo de la extracción es del harness (no de Gemini); de la API solo sale
el etiquetado de comunidades, 1 llamada.
