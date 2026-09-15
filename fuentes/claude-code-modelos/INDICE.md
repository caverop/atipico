# Claude Code: modelos, costo y ruteo por agente

Fuentes sobre cómo Claude Code elige modelo, cuánto cuesta cada uno, y cómo se configura por
agente. Se abrió el 2026-09-15 para decidir qué modelo ponerle a cada agente de `.claude/agents/`.

## Índice

| # | Fuente | URL | Capturada | Aporta | Tamaño |
|---|---|---|---|---|---|
| 01 | Pricing de Anthropic | [platform.claude.com/docs/en/about-claude/pricing](https://platform.claude.com/docs/en/about-claude/pricing) | 2026-09-15 | Opus 5 y Sonnet 5 cuestan 2,5× de diferencia, y los multiplicadores de caché | 2,7 KB |
| 02 | Model configuration (Claude Code) | [code.claude.com/docs/en/model-config](https://code.claude.com/docs/en/model-config) | 2026-09-15 | El `model:` del agente manda sobre el setting de sesión; existe el alias `opusplan` | 3,4 KB |
| 03 | Issue 54448: el override de modelo no rutea | [github.com/anthropics/claude-code/issues/54448](https://github.com/anthropics/claude-code/issues/54448) | 2026-09-15 | El reporte que decía que el frontmatter es inoperante — **contradicho por nuestra medición en 2.1.267** | 2,6 KB |

## Para qué se usó

Decidir el `model:` de los cuatro agentes de `.claude/agents/` (2026-09-15). Las tres fuentes
entraron en esa decisión: la 01 puso los números, la 02 el mecanismo y el alias `opusplan`, y la
03 obligó a medir en vez de creerle a un reporte.

## Medición propia (no es fuente de terceros)

Cinco sondas con `claude -p --output-format json`, leyendo `modelUsage`, el modelo **real de
inferencia**:

| agente | `model:` en el frontmatter | infirió con |
|---|---|---|
| `atipico` | `opusplan` | `claude-sonnet-5` |
| `db` | `opus` | `claude-opus-5` |
| `dev` | `sonnet` | `claude-sonnet-5` |
| `qa` | `sonnet` | `claude-sonnet-5` |

Conclusión: **el `model:` del frontmatter se respeta y gana sobre el `model` de los settings.**
La mitad "opus en modo plan" de `opusplan` **no** se pudo verificar headless (con
`--permission-mode plan` igual dio sonnet).

## ¿Vale un notebook?

**Todavía no.** 3 fuentes, tema acotado, y ya lo resolvimos. Se reabre si aparece una cuarta
consulta sobre configuración de modelos — por ejemplo al medir `effort`, que quedó pendiente.
