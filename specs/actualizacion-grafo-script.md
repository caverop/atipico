---
estado: descartado
ticket: sin ticket
actualizado: 2026-09-14
afecta: [specs]
---

# Actualización del grafo en un comando

**Hoy:** poner al día el grafo de `specs/` sigue siendo la receta a mano descrita en
`.claude/agent-memory/atipico/atipico-grafo-gemini-background.md` y en
`.claude/agents/atipico.md` — un agente arma el prompt rico, aplica los guardas y hace la
orquestación en el hilo principal. Este spec propuso mecanizar esa receta en un script
versionado; el usuario lo descartó el 2026-09-14 antes de que llegara a aprobación.

- **Estado:** **descartado.** El script (`tools/grafo-actualizar.py`, ~27 KB) llegó a
  escribirse sin haber pasado por aprobación —violando el propio orden spec → plan →
  aprobación → código que rige el proyecto— y el usuario lo rechazó explícitamente el
  2026-09-14. El archivo se borró; este spec queda solo como registro de por qué se
  propuso y por qué no se construye.
- **Alcance:** ninguno — ver Bitácora.
- **Fuera de alcance:** el hook o job desatendido (nunca llegó a evaluarse, quedó
  supeditado a que existiera el script) y el vault de Obsidian.

---

## 1. Problema que motivó la propuesta

La receta manual de actualizar el grafo depende de que quien la ejecute recuerde trece
guardas en prosa; nada verifica que se hayan cumplido. La corrida del 2026-09-14 los
cumplió todos y aun así el grafo quedó peor por dos motivos mecánicos (aristas perdidas
por archivo, manifest sellado con el subconjunto en vez del corpus completo) que un
script podría haber verificado sin criterio humano. Eso llevó a proponer
`tools/grafo-actualizar.py`: un comando que mecanizara los guardas mecánicos y reportara
los dos que exigen criterio (evidencia por nodo, coherencia de etiquetas de comunidad).

## 2. Por qué se descarta

El usuario decidió, el 2026-09-14, que no quiere automatización implementada para esta
tarea — más allá de si el diseño era correcto. La receta manual con guardas
(`.claude/agent-memory/atipico/atipico-grafo-gemini-background.md`) sigue vigente sin
cambios: sigue siendo el agente quien arma el prompt, aplica los guardas y hace el merge
a mano, en el hilo principal, cada vez que hace falta.

## 3. Bitácora

### 3.1 Diseños descartados

- **El script completo, `tools/grafo-actualizar.py`.** Se escribió antes de que este spec
  llegara a `aprobado`, saltándose el orden del proyecto. Descartado el 2026-09-14 por
  decisión explícita del usuario y borrado del árbol de trabajo.
- **El hook directo, sin script.** Ya descartado dos veces antes (2026-09-09 y
  2026-09-10): un hook no puede armar el prompt con guardas ni verificar ids, solo
  podría avisar — y el chequeo manual de deriva ya cumple ese rol.
- **Confiar en `extract_corpus_parallel(backend="gemini")` como documenta la skill.**
  Devolvió 15 nodos donde el grafo tenía 53, y `build_merge` reemplaza el archivo
  entero. Descartado por medición, no por opinión.
- **El prompt corto (`_EXTRACTION_SYSTEM`, 3301 chars).** Devolvió 11 nodos y con ids en
  otra forma (`numero_pedido_md` en vez de los conceptos del spec). Descartado.

### 3.2 Verificado empíricamente (2026-09-14)

Estos datos quedan como registro porque siguen siendo válidos para la receta manual,
aunque el script que los iba a mecanizar no se construya:

- Cuatro sondas sobre `numero-pedido.md` (53 nodos en el grafo): `extract_corpus_parallel`
  → **15**; prompt corto → **11**; prompt rico sin piso → **11** (5 ids coincidían);
  prompt rico + piso 48 + lista de ids → **53**, con 31 ids coincidentes, 22 con deriva y
  3 nodos marcados `verification=unverified`.
- Los 5 specs con deriva re-extraídos ese día: **0 ids perdidos** (11/11, 12/12, 12/12,
  83/83, 53/53), `source_file` limpio en los 175 nodos.
- Aristas de esos 5 archivos: **206 viejas contra 52 nuevas**; la unión dio 244, **0
  extremos inexistentes** — la extracción por archivo sola había perdido 154 de 206.
- Manifest: sellar con `detect_incremental()['files']` (todo el corpus) en vez del
  subconjunto tocado evita que se borren hashes de archivos intactos.
- Modelos: `gemini-3.8-flash` y `gemini-flash-latest` se cuelgan hasta el timeout;
  `gemini-2.5-flash` y `gemini-2.5-flash-lite` dan 404 pese a listarse; `gemini-3.6-flash`
  respondió en 2.8 s sin perder ids.

---

**En pocas palabras:** se propuso mecanizar la actualización del grafo en un script
versionado; el usuario lo descartó el 2026-09-14 y el script —que ya se había escrito sin
aprobación— se borró. La receta manual con guardas sigue siendo el único mecanismo.
