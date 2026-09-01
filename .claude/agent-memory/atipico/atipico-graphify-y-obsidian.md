---
name: atipico-graphify-y-obsidian
description: "Cómo se corre graphify en Atipico (sin clave de API, el LLM es el agente) y por qué el vault de Obsidian vive en specs/ y no puede incluir la memoria."
metadata:
  type: reference
---

## graphify

El grafo vive en `graphify-out/` en la **raíz** del repo, pero su `.graphify_root`
apunta a `specs/` — el corpus son los 10 specs, no el código. Se actualiza con
`graphify specs --update`.

**No hace falta ninguna clave de API.** La doc de la skill es tajante: *"graphify
needs no API key. Never ask the user for one, and never block on one."* Cuando
`GEMINI_API_KEY`/`GOOGLE_API_KEY` no está seteada — y en esta máquina **nunca lo
estuvo**, verificado en entorno de proceso, User y Machine de Windows, `settings.json`
y perfiles de shell — la extracción semántica la hace el agente anfitrión despachando
subagentes. Solo lee las de Gemini; no lee `ANTHROPIC_API_KEY` ni `OPENAI_API_KEY`,
aunque el mensaje de error del CLI sugiera lo contrario.

**La trampa:** correr el binario suelto en la terminal (`graphify specs --update`)
falla con *"no LLM API key found"*, porque ahí no hay agente que haga la parte
semántica. Me pasó el 2026-08-31 y lo reporté como bloqueador real cuando no lo era.
El corpus es 100% markdown, así que **no hay parte estructural que salve la corrida**:
código sí se extrae con AST local y sin clave, prosa no.

**Consecuencia:** la automatización desatendida es imposible sin clave de Gemini. Un
hook de git corre sin agente, así que un `post-commit` que dispare graphify sobre
`specs/` no puede funcionar. Se intentó y se descartó. El grafo se actualiza pidiéndolo
dentro de una sesión, que además encaja con [[atipico-spec-primero]]: cuando un spec
cambia, el agente ya está en la conversación.

Costo de referencia: una reconstrucción completa de los 10 specs registró ~202k tokens
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

## El costo de mover la carpeta de specs

Los specs están citados desde el código: al renombrar `docs/` → `specs/` hubo que
tocar **55 referencias en 37 archivos** — controladores, entidades, configuraciones de
EF, tests, migraciones SQL, `app.css`, `render.yaml`, `docker-compose.yml`,
`.env.example`. Ojo con un `sed` ciego sobre `docs/`: Bootstrap vendorizado en
`wwwroot/lib/` trae URLs `getbootstrap.com/docs/` que no hay que tocar. Filtrar por el
patrón `docs/<nombre>.md` y excluir `wwwroot/lib/`.

## Trampas de la corrida incremental (2026-08-31)

**Los ids del grafo quedaron partidos en dos formatos por el rename `docs/` → `specs/`.**
Al 2026-08-31, 35 nodos conservan ids con prefijo `docs_` (p. ej.
`docs_direccion_entrega_columna_en_dos_lugares`): son los de los 5 specs que **no**
cambiaron desde la corrida anterior. Los 5 re-extraídos ya tienen ids con el stem nuevo.
No hay aristas colgantes hoy, pero cuando esos 5 se re-extraigan van a **duplicar** en vez
de reemplazar, porque el merge empareja por id. La corrección que indica la propia skill es
`graphify extract --force` (reconstrucción completa). **Aún no se corrió** — decisión
pendiente del usuario.

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
