# Issue 54448 — "Subagent model override is silently inoperative"

- **URL:** https://github.com/anthropics/claude-code/issues/54448
- **Capturada:** 2026-09-15
- **Tipo:** issue de GitHub (reporte de bug, cerrado como duplicado)
- **Para qué la usé:** la búsqueda lo devolvió como evidencia de que el `model:` del frontmatter
  no funciona. **Se usó en contra**: obligó a medir en vez de creerle, y la medición lo desmintió
  en la versión instalada.

## Extracto

> **What's Wrong?** Claude Code documents two ways to set a subagent's model: `model:` field in
> agent definition frontmatter … and `model` parameter on the Agent tool call. **Empirically,
> neither controls actual model routing. Subagents always inherit the parent session's model.**

> This is a silent failure: the agent self-reports the configured model in conversation ("I am
> running on Claude Opus 4.7"), but inference goes to the parent's model.

> **Is this a regression?** No, it never worked

> 100% of actual inference calls went to Sonnet, despite both frontmatter and explicit Agent tool
> param requesting Opus.

> The current workaround is for the user to manually run `/model` to switch the parent session
> before each subagent invocation.

Metadatos del reporte:

- **Versión donde se reportó:** 2.1.119 · macOS · Anthropic API
- **Creado:** 2026-04-28 · **Cerrado:** 2026-05-02, como **duplicado** (`state_reason: duplicate`,
  hilo bloqueado). Cerrado como duplicado no significa arreglado: significa que hay otro hilo
  siguiéndolo.

## Datos que me llevé

- El síntoma que describe es el peor posible para diagnosticar: **el agente dice que corre en el
  modelo configurado y no es cierto**, así que investigar una caída de calidad no da ninguna
  señal dentro del producto.
- El método de verificación del reporte es el correcto y es el que adoptamos: mirar los registros
  reales de inferencia, no lo que el agente declara. En nuestro caso, `modelUsage` de
  `claude -p --output-format json`.

## Qué NO dice / límites

- **Es de la 2.1.119 y nosotros corremos la 2.1.267.** Nuestra medición del 2026-09-15 lo
  contradice: `db` con `model: opus` infirió con `claude-opus-5`, y `atipico` con `model:
  opusplan` con `claude-sonnet-5`. **El campo sí rutea en la versión instalada.**
- No incluye respuesta de Anthropic en el hilo (cerrado y bloqueado), así que no se sabe en qué
  versión se arregló ni si el duplicado sigue abierto.
- No cubre agentes sueltos (frontmatter de `.claude/agents/*.md` invocados con `--agent`): el
  reporte habla de subagentes invocados por la tool `Agent`. Son mecanismos distintos.
