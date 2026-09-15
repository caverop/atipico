# Model configuration — Claude Code

- **URL:** https://code.claude.com/docs/en/model-config
- **Capturada:** 2026-09-15
- **Tipo:** documentación oficial
- **Para qué la usé:** saber si el `model:` del frontmatter de un agente manda sobre el setting
  de sesión, y descubrir si existía un alias que diera "modelo fuerte para planificar, barato
  para ejecutar".

## Extracto

Precedencia del modelo de sesión, en orden:

> 1. **During session**: use `/model <alias|name>` to switch immediately …
> 2. **At startup**: launch with `claude --model <alias|name>`
> 3. **Environment variable**: set `ANTHROPIC_MODEL=<alias|name>`
> 4. **Settings**: configure permanently in your settings file using the `model` field
> 5. **Default for new sessions**: set `ANTHROPIC_DEFAULT_MODEL=<alias|name>`

Alias disponibles (tabla de la doc, fila que importa):

> **`opusplan`** — Special mode that uses `opus` during plan mode, then switches to `sonnet` for
> execution

> | Provider | `opus` | `sonnet` |
> | Anthropic API | Opus 5 | Sonnet 5 |

> Opus 5 requires Claude Code v2.1.219 or later. Sonnet 5 requires v2.1.197 or later.

Resolución del modelo de un subagente:

> **`CLAUDE_CODE_SUBAGENT_MODEL`** — The default model for subagents, agent team teammates, and
> workflow agents that aren't assigned a model another way. Accepts an alias such as `haiku` or a
> full model name. **A per-invocation model or a definition's `model` field, including
> `inherit`, takes precedence.** To change that, set `CLAUDE_CODE_SUBAGENT_MODEL_FORCE`

> The check also covers a `model` set in **subagent frontmatter**.

Cómo saber qué modelo corrió de verdad:

> The stderr warning is suppressed for `--output-format json` and `stream-json`; read the actual
> model from the **`modelUsage`** field of the result message instead.

Contexto:

> On the Anthropic API, Sonnet 5 **always** runs with the 1M context window. There is no 200K
> variant, no `[1m]` suffix to select, and no usage credits required on any plan.

## Datos que me llevé

- **El `model:` del agente gana** sobre el `model` de los settings: lo dice la doc para
  subagentes, y la medición propia de este tema lo confirmó para agentes.
- Existe **`opusplan`**: opus en modo plan, sonnet en ejecución. Es exactamente el patrón
  "caro para decidir, barato para ejecutar", en una palabra.
- `CLAUDE_CODE_SUBAGENT_MODEL` fija el default de los subagentes; `_FORCE` lo impone a todos
  ignorando el frontmatter.
- `modelUsage` en la salida JSON (`-p --output-format json`) es la forma de **medir** qué modelo
  infirió, en vez de preguntarle al agente (que reporta lo que tiene configurado, no lo que corre).
- `default`, `inherit`, `opusplan` y `haiku` son ignorados por `ANTHROPIC_DEFAULT_MODEL`.

## Qué NO dice / límites

- **No dice cómo detecta el "plan mode"** que activa la mitad opus de `opusplan`. Probamos
  `--permission-mode plan` headless y siguió en sonnet: o el modo plan no se activa sin terminal
  interactiva, o el alias mira otra señal. Sin resolver.
- Menciona `modelSettings` y `effortLevel` (low/medium/high/xhigh) como perillas de esfuerzo,
  pero **no leí esa sección completa**: el resto de la página quedó fuera del recorte que traje.
  Queda pendiente si se quiere medir el eje de `effort`.
- No compara calidad entre modelos. La doc dice qué existe y cómo se configura, no qué conviene
  para un trabajo concreto.
