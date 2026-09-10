---
name: atipico-graphify-y-obsidian
description: "Cómo se corre graphify en Atipico (sin clave de API, el LLM es el agente) y por qué el vault de Obsidian vive en specs/ y no puede incluir la memoria."
metadata:
  type: reference
---

## graphify

El grafo vive en `graphify-out/` en la **raíz** del repo, pero su `.graphify_root`
apunta a `specs/` — el corpus son los specs, no el código.

**Estado al 2026-09-09 (SCRUM-27):** 389 nodos · 502 aristas · 20 comunidades sobre
los 25 documentos. Antes eran 177 sobre 10.

**El grafo se atrasa en silencio y nada avisa.** Llegó a tener 10 de 24 specs con 8
días de deriva. Nada lo detecta solo (no hay hooks, el CI no mira `specs/`), así que
hay que ir a buscarlo: comparar las claves de `graphify-out/manifest.json` contra los
`.md` de `specs/`. Ojo con `graph.json`: las aristas están bajo **`links`**, no
`edges` — `len(d['nodes'])` con `d.get('edges')` da "0 aristas" y parece un grafo roto
cuando no lo está.

**Un `--update` después de actualizar graphify cuesta como una reconstrucción
completa.** La caché semántica atribuye cada entrada al prompt que la produjo
(`references/extraction-spec.md`); si ese archivo cambió con una versión nueva, la
caché da **0 hits** y se re-extrae todo. Verificado el 2026-09-09: `detect_incremental`
marcó 25 cambiados cuando solo 15 eran nuevos —los otros 10 por `mtime`— y el chequeo
de caché no rescató ninguno. Costó **812k tokens** (6 subagentes, ~130-150k cada uno)
contra los ~111k de la corrida anterior de 10 documentos. **Chequear el hit-rate de la
caché antes de despachar subagentes**, no después: si da 0, el costo real es el del
corpus entero y conviene decirlo antes de arrancar.

**El efecto colateral bueno:** al re-extraerse los 25 juntos, todas las citas entre
specs quedaron enlazadas. La arista `direccion-entrega` → `tipo-pedido`, colgada desde
agosto por haberse extraído en corridas distintas, ya existe.

**Encoding en Windows:** los scripts de graphify imprimen etiquetas con acentos y la
consola es cp1252 — un `print` de labels revienta con `UnicodeEncodeError`. Correr
siempre con `PYTHONIOENCODING=utf-8`. Y para pasos con mucho texto acentuado, escribir
un `.py` al scratchpad y ejecutarlo, en vez de `python -c` con comillas anidadas.

**No hace falta ninguna clave de API.** La doc de la skill es tajante: *"graphify
needs no API key. Never ask the user for one, and never block on one."* Cuando
`GEMINI_API_KEY`/`GOOGLE_API_KEY` no está seteada — y en esta máquina **nunca lo
estuvo**, verificado en entorno de proceso, User y Machine de Windows, `settings.json`
y perfiles de shell — la extracción semántica la hace el agente anfitrión despachando
subagentes. Solo lee las de Gemini; no lee `ANTHROPIC_API_KEY` ni `OPENAI_API_KEY`,
aunque el mensaje de error del CLI sugiera lo contrario.

**El binario suelto no hace la parte semántica, pero tampoco falla.** Verificado el
2026-09-01 con backup: `graphify update specs` termina con exit 0 y anuncia *"no LLM
needed"*. Lo que hace es una extracción **estructural del markdown por encabezados**, que
no es lo mismo: dio 248 nodos superficiales contra los 123 semánticos del grafo real. Y
los escribe en **`specs/graphify-out/`**, no en el `graphify-out/` de la raíz que es el
canónico — te quedan dos grafos distintos sin avisarte. No lo uses para actualizar.
(Antes esta nota decía que fallaba con *"no LLM API key found"*; eso era de una versión
anterior y ya no es cierto — el modo silencioso es peor que el error.)

**Consecuencia:** la automatización desatendida es imposible sin clave de Gemini. Un
hook de git corre sin agente, así que un `post-commit` que dispare graphify sobre
`specs/` no puede funcionar. Se intentó y se descartó. El grafo se actualiza pidiéndolo
dentro de una sesión, que además encaja con [[atipico-spec-primero]]: cuando un spec
cambia, el agente ya está en la conversación.

Costo de referencia (histórico, corpus de 10): una reconstrucción completa registró ~202k tokens
de entrada en `graphify-out/cost.json`.

## Obsidian

El vault es `specs/` (su `.obsidian/` vive adentro). Solo plugins core, sin plugins de
comunidad, sin Dataview — así que lo que se ve renderizado y lo que se lee del disco es
lo mismo. Las skills de NotebookLM (`notebooklm`, `study*`) no tienen nada que ver: usan
automatización de navegador con login de Google, no la API de Gemini.

**Obsidian ignora las carpetas que empiezan con punto.** Por eso la memoria
(`.claude/agent-memory/`) **no puede** entrar en ningún vault, ni subiéndolo a la raíz
del repo. Se evaluó unificar specs + memoria en un solo grafo el 2026-08-31 y se
descartó por esto.

**Enlaces entre specs: markdown relativo, nunca `[[wikilinks]]`.** El repo está en
GitHub, que renderiza los wikilinks como texto literal; un enlace relativo
`[texto](otro-spec.md)` funciona en Obsidian, en GitHub y al leerlo como texto. Al
2026-08-31 hay 8 enlaces y 2 specs aislados: `numero-pedido.md` (el más grande, 60 KB)
y `deploy-azure-aspire.md`.

## El export a Obsidian y su bucle de realimentación (2026-09-01)

`graphify export obsidian` escribe **una nota por nodo** (136 archivos: 123 nodos + 13
comunidades) más un `graph.canvas`. Se corrió por primera vez el 2026-09-01 y vive en
**`specs/grafo/`**, un subdirectorio del vault: así se ve en la misma ventana de Obsidian
que los specs, sin mezclarse con los 10 archivos reales de arriba.

**No hay colisión de nombres** con los specs (las notas se llaman por su etiqueta y las de
comunidad llevan prefijo `_COMMUNITY_`), pero **el export trae su propio `.obsidian/`** que
pisaría la configuración del vault. Al copiarlo hay que excluirlo: copiar solo `*.md` y
`graph.canvas`, nunca la carpeta entera.

**La trampa que importa: el grafo se come su propia salida.** El corpus de graphify es
`specs/`, así que apenas se copian las notas ahí, el siguiente `--update` las detecta como
136 documentos nuevos y extrae un grafo de su propio export. Verificado empíricamente el
2026-09-01: `detect_incremental` pasó de 0 a 136. La solución fue una entrada
`specs/grafo/` en el `.gitignore` de la raíz — graphify lee `.gitignore` y
`.graphifyignore` de toda la cadena de ancestros, así que una sola línea sirve para las dos
cosas (no entra al repo y no se re-extrae). Confirmado: volvió a 0.

**Cómo regenerarlo** cuando cambien los specs: exportar a un directorio de descarte,
inspeccionar, y copiar solo los `.md` y el canvas a `specs/grafo/`. El export lleva
`[[wikilinks]]`, que el repo prohíbe en los specs escritos a mano porque GitHub los
renderiza literales; en las notas generadas no molesta porque están gitignoreadas y nunca
llegan a GitHub.

## El costo de mover la carpeta de specs

Los specs están citados desde el código: al renombrar `docs/` → `specs/` hubo que
tocar **55 referencias en 37 archivos** — controladores, entidades, configuraciones de
EF, tests, migraciones SQL, `app.css`, `render.yaml`, `docker-compose.yml`,
`.env.example`. Ojo con un `sed` ciego sobre `docs/`: Bootstrap vendorizado en
`wwwroot/lib/` trae URLs `getbootstrap.com/docs/` que no hay que tocar. Filtrar por el
patrón `docs/<nombre>.md` y excluir `wwwroot/lib/`.

## Trampas de la corrida incremental (2026-08-31)

**Los ids `docs_*` ya se limpiaron (2026-09-01).** Durante un tiempo el grafo tuvo dos
formatos de id conviviendo, por el rename `docs/` → `specs/`: 35 nodos con prefijo `docs_`
y el resto sin él. Se corrigió renombrando en `graph.json` —quitando el prefijo, **no**
cambiándolo por `specs_`: la raíz de escaneo *es* `specs/`, así que el stem no la
incluye—. Fue seguro: 0 colisiones, 123 nodos y 155 aristas intactos, 0 aristas colgantes.
El caché nunca estuvo contaminado (tiene solo entradas con ids nuevos), así que no hizo
falta ningún `extract --force`.

**Efecto colateral ya visible:** `direccion-entrega.md` §5.5 cita explícitamente a
`tipo-pedido.md` §5.1, pero su nodo quedó con **grado 0**. La arista `cites` nunca se creó
porque cuando se extrajo `direccion-entrega.md` el nodo destino no existía todavía. Las
citas entre specs solo se enlazan si ambos lados se extraen en la misma corrida, o si el
que cita se re-extrae después.

**`cd` en Bash persiste entre llamadas y rompe graphify.** Un `cd graphify-out` para leer
el reporte dejó el cwd ahí, y el siguiente `graphify reflect` creó un
`graphify-out/graphify-out/` anidado. Todas las rutas de graphify son relativas al cwd.
Correr siempre desde la raíz del repo, con rutas absolutas.

**Costo de referencia incremental:** 5 specs cambiados = ~145k tokens (vs. ~202k la
reconstrucción completa de los 10). El host reporta un **único número combinado** de
tokens del subagente, no el desglose entrada/salida, así que en `cost.json` queda todo
como `input_tokens` y el `output` en 0. No es que no haya habido salida: el dato separado
no existe.
