---
name: atipico-grafo-gemini-background
description: "La extracción del grafo va a la API de Gemini (no a subagentes Haiku), pero solo funciona con el prompt rico y los guardas inyectados por el orquestador: la llamada pelada que documenta la skill devuelve ~20% de los nodos y mergearla destruye el archivo."
metadata:
  type: feedback
---

# Extracción del grafo: Gemini por API, con los guardas en el orquestador

**Pedido del usuario el 2026-09-14:** *"que ahora se use gemini en vez de haiku"*. La clave
`GEMINI_API_KEY` está configurada (scope User de Windows, free tier de Google) y verificada
funcionando.

## Qué cambió y qué no

- **Cambió:** la extracción semántica ya no se despacha a un subagente con el Agent tool. Se
  hace contra la API de Gemini, un archivo por llamada, sin `run_in_background` y sin Agent.
- **No cambió:** la orquestación. Mergear, rebuildear, verificar contra el respaldo, podar
  duplicados y commitear los sigue haciendo el agente principal, en vivo, en el hilo. Ahí
  vive el control y no es delegable — ni siquiera ahora que la extracción es "una llamada".

## Lo medido el 2026-09-14, que es toda la trampa

Cuatro sondas sobre `specs/numero-pedido.md`, que en el grafo tiene **53 nodos**:

| llamada | nodos | ids que coinciden con el grafo |
|---|---|---|
| `extract_corpus_parallel(files, backend="gemini")` — **lo que documenta la skill** | 15 | 0 |
| `extract_files_direct(..., backend="gemini")` — prompt default (`_EXTRACTION_SYSTEM`, 3301 chars) | 11 | 0 |
| prompt rico (`references/extraction-spec.md`), sin piso de nodos | 11 | 5 |
| prompt rico + **piso 48** + lista literal de los 53 ids | **53** | 31 |

**La conclusión que hay que leer antes de tocar nada:** la skill afirma que, con
`GEMINI_API_KEY` seteada, se usa `extract_corpus_parallel(files, backend="gemini")` *"instead
of dispatching subagents"*. **No es equivalente.** Esa llamada pelada devuelve 20-30% de los
nodos, y como `build_merge` **borra todos los nodos cuyo `source_file` es el archivo
re-extraído** y pone los nuevos, mergearla habría destruido 48 de los 53 nodos de ese
archivo. Sobre los 5 specs que tenían deriva ese día eran ~119 de 171.

Los guardas no son una precaución de modelo barato: **son la única razón por la que la ruta
API alcanza la cobertura correcta.** Con el prompt default, Gemini no es peor que Haiku
extrayendo — es que está haciendo otra tarea: devuelve artefactos y un nodo por documento
(`numero_pedido_md`, `atipico_api_controllers_apicontrollerbase`) en vez de los conceptos del
spec (`numero_pedido_uk_turno_caja_abierto`).

Lo que arregla cada pieza, medido y no supuesto:

- **El prompt rico arregla la *forma* de los ids, no la *cantidad*.** Sin piso, 11 nodos, pero
  5 de esos 11 ya coincidían con ids existentes (contra 0 con el default): deja de mintear
  duplicados estructurales.
- **El piso explícito arregla la cantidad.** De 11 a 53, con el conteo exacto.
- **La deriva de ids no se arregla con nada de eso.** 22 de los 53 quedaron distintos (mismo
  concepto, entidad fraseada distinto), y graphify marcó **3 nodos como
  `verification=unverified`** por *"no evidence in the source"*. Por eso la comparación
  perdidos/nuevos y la poda de duplicados siguen siendo obligatorias.

## La receta exacta

Uno o pocos archivos por llamada — pero **no de a uno suelto**, por lo del punto 1 de la
corrida real más abajo: así se pierden las aristas entre specs. `_EXTRACTION_SYSTEM` es una
constante de módulo y
`_extraction_system()` la lee **en tiempo de llamada**, así que se puede inyectar el prompt
rico sin parchear el paquete instalado:

```python
import graphify.llm as m
from pathlib import Path

# 1. El prompt rico, del bloque de código de references/extraction-spec.md
spec = Path(".dsh/skills/graphify/references/extraction-spec.md").read_text(encoding="utf-8")
prompt = spec[spec.index("```") + 3 : spec.rindex("```")].strip()
prompt = prompt.replace("TOTAL_CHUNKS", "1").replace("CHUNK_NUM", "1")
prompt = prompt.replace("FILE_LIST", "numero-pedido.md")          # el path verbatim
prompt = prompt[: prompt.find("Then write the JSON to disk")].rstrip()  # la API no escribe a disco

# 2. Los dos guardas que hacen la diferencia
prompt += (
    f"\n\nCOVERAGE FLOOR: la extracción previa verificada de este documento dio {n} nodos. "
    f"Si emitís menos de {n - 5} sub-extraíste: releé el documento antes de responder. "
    "Cada concepto, entidad, mecanismo, regla o decisión con nombre propio merece su nodo."
    f"\n\nREUSE THESE EXISTING NODE IDS VERBATIM: {', '.join(ids_existentes)}"
)

# 3. Inyectar y llamar
m._EXTRACTION_SYSTEM = prompt
res = m.extract_files_direct([Path("specs/numero-pedido.md")], backend="gemini", root=Path("specs"))
```

Ojo con el orden de las sustituciones: `TOTAL_CHUNKS` antes que `CHUNK_NUM` (si no,
`TOTAL_CHUNKS` queda con el `1` adentro del otro).

## Los guardas son obligatorios, corra quien corra la extracción

**Antes de extraer**

1. **Chequeá la deriva primero**, y decila antes de empezar:
   `detect_incremental(Path('specs'))` — es la respuesta autoritativa, detecta contenido
   cambiado, no solo archivos nuevos.
2. **Mirá el hit-rate de la caché antes de llamar.** Si da 0 hits, el prompt de extracción
   cambió con una versión nueva de graphify y se re-extrae el corpus entero, no solo lo que
   tocaste. Decí el costo antes de arrancar, no después.

**En el prompt**

3. **Piso explícito de nodos**, basado en la corrida anterior del mismo archivo. Es *el* guarda
   que cierra la brecha de cobertura con la API.
4. **Lista literal de los ids existentes a reusar verbatim**, sacada de `graph.json` antes de
   llamar.
5. **Pedí el documento completo, nunca "qué cambió".** Un prompt centrado en el cambio
   devolvió 30 nodos donde el spec tenía 39 y habría borrado los candidatos A/B/C, la regla de
   subconjunto y el orden de autoridad. Los cambios se mencionan como hechos a acertar, jamás
   como el alcance.
6. **"No hagas X" no garantiza que no lo haga, ni repitiéndolo dos veces en el mismo prompt.**
   El prompt de `README.md` decía explícito, dos veces, "no crear un nodo por fila de la
   tabla", y salieron 25 igual (`readme_spec_<nombre>`), duplicando estructuralmente nodos que
   cada spec ya tiene.

**Antes de aceptar el merge**

7. **`Counter(n['source_file'] for n in res['nodes'])`, siempre, antes de mirar otra cosa.** El
   typo `pdiego`/`pdieg` salió tres corridas seguidas, y en una el propio extractor lo dio por
   corregido en su resumen. La ruta escrita mal mete un archivo entero bajo el `source_file`
   incorrecto.
8. **Respaldo de `graph.json` a `.graphify_old.json` antes de mergear, y comparación de ids
   perdidos/nuevos contra ese respaldo.** Es lo único que atrapa una extracción corta *y* una
   omisión puntual de un id de la lista de reuso: el piso total puede seguir cumplido aunque
   falte exactamente el nodo que pediste.
9. **Al podar duplicados por prefijo de etiqueta, no te lleves un id legítimo de una corrida
   anterior** que coincida con el patrón textual. Pasó con `readme_spec_postgres_local_dev`,
   que era real: hubo que recuperarlo de `graph.json` antes del merge. Contá cuántos nodos
   comparten el patrón sospechoso antes de podar, no solo el total. La poda va **por prefijo de
   etiqueta, no por similitud de id** — la similitud dio 6 falsos positivos de 7.
10. **Verificá el disco, no el resumen.** Que el chunk esté bien escrito en `graphify-out/` es
    el único hecho; el mensaje final puede decir cualquier cosa (y un chunk cortado por límite
    de sesión alcanzó a escribir completo).

**Después del merge**

11. **`build_merge` no escribe `graph.json` en disco — solo `to_json()` (dentro de
    `rebuild.py`) lo hace.** Para restaurar un nodo perdido, escribí sobre
    **`.graphify_extract.json`**, nunca sobre `graph.json`: ese todavía tiene el estado *antes*
    del merge y escribirle ahí pisa el trabajo recién hecho sin que se note hasta el rebuild.
12. **Nunca saltees el etiquetado de comunidades (Step 5), ni para 2 archivos.** `cluster()`
    reordena comunidades enteras aunque solo cambien 2 archivos de 25, así que el id 1 de esta
    corrida no es el mismo grupo que el id 1 de la anterior; saltear el paso deja
    `community_name` con el **texto viejo pegado al id nuevo**, y `graph.json` sigue siendo JSON
    válido, así que nada avisa. **Chequeo obligatorio antes de dar el grafo por al día:** tomá
    3-5 ids de comunidad al azar (no solo las tocadas) y compará el label contra 3-4 nodos
    reales de esa comunidad.
13. **`encoding='utf-8'` explícito al escribir y al leer cualquier temporal** con acentos: en
    Windows el default puede no ser UTF-8 y revienta con `UnicodeDecodeError` al releerlo.

**Cláusula de proporcionalidad — con el costo cambiado.** Antes una corrida costaba ~90-110k
tokens de contexto de agente, y por eso un cambio que no le enseñaba nada al grafo se difería.
Ahora una extracción por API sale ~14k tokens (~$0.019 en tier pago, $0 en free tier): **el
argumento de costo dejó de morder.** La regla sigue en pie por ruido del grafo, no por tokens —
"esto no le enseña nada al grafo, lo difiero" sigue siendo válido, pero ya no se sostiene solo
en el precio. Si el usuario quiere que la proporcionalidad se relaje ahora que es gratis, lo
dice él.

## La primera corrida real por API (2026-09-14): salió bien y salió mal

Los 5 specs con deriva se re-extrageron y mergearon. Lo bueno: **0 ids perdidos** en los cinco
(11/11, 12/12, 12/12, 83/83, 53/53 más 3 nodos nuevos de triggers), `source_file` limpio en
todos —el typo `pdiego` de Haiku no apareció—, grafo final en 609 nodos / 766 aristas / 35
comunidades y **deriva 0**. Cuatro cosas salieron mal, y **ninguna la habría atrapado el guard
de nodos**:

1. **La extracción por archivo suelto no produce las citas ENTRE specs, y `build_merge` BORRA
   las aristas viejas del archivo re-extraído** (reemplaza por tier, no por nodo). Medido: esos
   5 archivos tenían **206 aristas** en el grafo y la extracción por archivo devolvió **52**; el
   total cayó de 757 a 603 y el guard de nodos no dijo nada, porque no se perdió un solo nodo.
   **Guarda nueva: contar aristas antes y después, no solo nodos.** Arreglo aplicado: como no se
   perdió ningún id, los extremos de las viejas seguían existiendo, así que se reincorporaron las
   206 (unión deduplicada = 244) y el grafo quedó en 766 aristas, **9 más que antes**. Para
   evitarlo de raíz: extraer los archivos **en un mismo chunk** (`token_budget` de 60k), que es
   como los agrupa el pipeline.

2. **El número de comunidades delata la conectividad.** Con las aristas perdidas el grafo se
   fragmentó en **148 comunidades**; al restaurarlas bajó a **35**, mucho más coherente para 26
   documentos. Si una corrida dispara el conteo de comunidades, sospechar de aristas perdidas
   antes que de un cambio real en el corpus.

3. **El manifest se rompe en silencio.** `detect_incremental(...)['files']` trae los **26
   documentos**, no el subconjunto cambiado (eso viene en `new_total`). La fórmula de la skill
   (`dispatched - stamped` para `clear_semantic`) le borró el `semantic_hash` a 21 archivos que
   no se habían tocado, y la deriva pasó de 5 a **21**. Se arregla re-sellando el corpus
   completo con `_stamped_manifest_files(corpus_completo, extraccion_completa, root)` +
   `save_manifest(..., clear_semantic=None)`, y se verifica con `detect_incremental` en **0**.

4. **El modelo se elige probando, no leyendo la lista.** `/v1beta/models` lista 41 modelos con
   `generateContent`, pero no todos sirven: `gemini-3.8-flash` y `gemini-flash-latest` **se
   cuelgan** hasta el timeout —y el default de graphify es **600 s**, así que un modelo muerto
   cuelga la corrida diez minutos, que es exactamente lo que pasó—, y `gemini-2.5-flash` /
   `gemini-2.5-flash-lite` devuelven **404 "no longer available"** aunque estén en la lista.
   `gemini-3.6-flash` anduvo: 2.8 s en un ping y la extracción completa sin un id perdido.
   **Ping barato antes de la corrida, y `GRAPHIFY_API_TIMEOUT` seteado** (150 s alcanza).

5. **A escala de corpus, el camino del CLI colapsa a un nodo por documento.** Medido el
   2026-09-14, tras borrar `graphify-out/` para reconstruir de cero:
   `graphify extract specs --backend gemini` sobre **28 documentos devolvió 28 nodos** — ids que
   son el stem del archivo (`numero_pedido`, `reservas`) y labels que son el título del spec.
   Terminó con **exit 0** y un reporte que parece sano. Es el mismo defecto del prompt corto de
   la sonda de arriba, a escala: la firma es **~1 nodo por archivo**, y así se reconoce. Además
   `--out` es el directorio *padre*, no el destino: `--out graphify-out` escribe
   `graphify-out/graphify-out/graph.json`. **Una reconstrucción en frío no se hace con el CLI**:
   se hace con el prompt rico y lotes de ~6 documentos con piso por densidad. El procedimiento
   completo está en `.agents/skills/actualizar-grafo/SKILL.md` §8.

6. **La ruta API trunca cada archivo a 20.000 caracteres — y eso invalida la lectura optimista
   de los pisos.** `graphify.llm._FILE_CHAR_CAP = 20000` y `_read_files` hace
   `content[:_FILE_CHAR_CAP]`: medido el 2026-09-15, `numero-pedido.md` (61.678 bytes) mandó
   **7.865** tokens de entrada con el cap y **20.025** con el cap en 500.000 — el modelo veía un
   tercio del spec. Por eso el "**53 nodos, 31 ids coincidentes**" del 2026-09-14 **no prueba
   que los guardas cierren la brecha**: con el documento truncado y los 53 ids servidos en el
   prompt, el modelo tenía de dónde copiarlos. Con el cap parcheado y **sin** lista de ids, ese
   archivo dio **26 nodos** (0,43 nodos/KB, la mitad del histórico). **Parchear
   `_FILE_CHAR_CAP` es obligatorio antes de extraer**, y el techo de densidad de la ruta API es
   ~0,43 nodos/KB: el grafo de 627 lo hicieron subagentes que leían los archivos, no requests
   sueltos. Detalle y mediciones en `.agents/skills/actualizar-grafo/SKILL.md` §8.5.

7. **La reconstrucción por subagentes funcionó, y es la que hay que usar (2026-09-15).** 7
   subagentes de DSH —uno por grupo temático del índice de `specs/README.md`, ~4 specs cada uno—,
   cada uno leyendo sus archivos **enteros**, con el prompt rico y un piso por archivo:
   **1302 nodos y 1635 aristas** sobre 28 documentos, contra 627/805 del grafo anterior.
   `numero-pedido` pasó de 53 a **140** nodos; `deploy-azure-aspire` de 13 a **67**;
   `formato-spec` de 15 a **40**. Sin ids inválidos, sin aristas colgantes, sin nodos sin
   evidencia, y el grafo quedó en **1618 aristas / 132 comunidades**, con el vault en 1434 notas.
   **Conclusión: el grafo viejo estaba flaco, no este inflado** — la ruta API trunca a 20.000
   caracteres y un request suelto no lee un spec de 60 KB; un subagente sí. **Para reconstruir,
   subagentes; la API queda para lo incremental** (`SKILL.md` §8.6).

Además: la **cuota del free tier es por modelo**, así que agotar `gemini-3-flash-preview`
(4 × 429 más un 503 después de ~6 llamadas) no bloquea a los estables; y el 503 *"high demand"*
es transitorio, vale reintentar. Costo de toda la corrida: **$0** (35k tokens de entrada, 23k de
salida).

**Los borradores de esa corrida ya no existen.** Vivían en `graphify-out/` (gitignoreado) y ese
directorio se borró entero el 2026-09-14 para reconstruir el grafo de cero, así que
`.graphify_gemini_merge.py` y `.graphify_gemini_fix.py` se fueron con él. La idea del script
reproducible también quedó descartada por el usuario ese día
(`specs/actualizacion-grafo-script.md`, `descartado`): el procedimiento vive en la skill
`.agents/skills/actualizar-grafo/`, y lo ejecuta el agente cuando el usuario lo pide.

## Entorno: lo que hay que saber para no re-descubrirlo

- **La clave** está en scope User: `[Environment]::GetEnvironmentVariable('GEMINI_API_KEY','User')`.
  No hace falta pedírsela al usuario ni volver a configurarla.
- **El venv de graphify necesitó `openai` + `tiktoken` a mano.** El backend `gemini` va por el
  endpoint compatible con OpenAI (`generativelanguage.googleapis.com/v1beta/openai/`), así que
  el extra `[gemini]` son esos dos paquetes. El venv de uv **no trae pip** (uv los crea sin
  sembrar) y **uv ya no está instalado** en esta máquina: quedó una entrada fantasma en el PATH
  (`astral-sh.uv_Microsoft.Winget.Source_8wekyb3d8bbwe`, el directorio no existe). Se resolvió
  con `python -m ensurepip --upgrade` + `pip install openai tiktoken` sobre
  `graphify-out/.graphify_python`, que necesita escritura fuera del workspace (aprobación de
  sandbox). **Si algún día se reinstala uv y se corre `uv tool upgrade graphifyy`, esos dos
  paquetes se pierden:** reinstalar con `uv tool install "graphifyy[gemini]" --force`.
- **`PYTHONIOENCODING=utf-8`** obligatorio, y **no alcanza con `PYTHONUTF8=1`**: los scripts
  imprimen acentos y la consola de Windows es cp1252, así que un `print` de labels revienta con
  `UnicodeEncodeError` (verificado de nuevo el 2026-09-14: con `PYTHONUTF8=1` el script igual se
  cortó a mitad de imprimir comunidades).
- **En DSH, el proceso que ya está corriendo no ve una variable recién seteada** (el bloque de
  entorno se hereda al arrancar). Puente para cada comando:
  `$env:GEMINI_API_KEY = [Environment]::GetEnvironmentVariable('GEMINI_API_KEY','User')`.
- **La extracción NO toca `graph.json`.** Verificado por hash en las cuatro sondas: extraer y
  mergear son pasos separados, así que se puede medir sin riesgo. Aprovechalo — es lo que
  permite comparar contra el respaldo *antes* de escribir nada.
- **Corré siempre desde la raíz del repo, con rutas absolutas.** Un `cd graphify-out` deja el cwd
  ahí y el siguiente comando crea `graphify-out/graphify-out/` anidado.
- **En `graph.json` las aristas viven bajo `links`, no `edges`.** `len(d.get('edges'))` da 0 y
  parece un grafo roto cuando no lo está.

## Cerrado

**El script reproducible.** Hoy la receta vive acá y se reconstruye a mano cada vez, y eso
queda así. Se llegó a escribir `tools/grafo-actualizar.py` (un script versionado que recibía
los archivos, leía el piso y los ids del `graph.json`, armaba el prompt y llamaba a la API) sin
que el spec que lo proponía (`specs/actualizacion-grafo-script.md`) pasara por aprobación. El
usuario lo rechazó explícitamente el 2026-09-14: se borró el archivo y el spec quedó
`descartado`. **No proponer de nuevo** un script, hook o job desatendido para esto salvo que el
usuario lo pida — ver [[atipico-grafo-con-el-commit]].

Relacionado: [[atipico-grafo-con-el-commit]], [[atipico-graphify-y-obsidian]].
