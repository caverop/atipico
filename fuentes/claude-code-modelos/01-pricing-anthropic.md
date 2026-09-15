# Pricing de Anthropic (lista oficial)

- **URL:** https://platform.claude.com/docs/en/about-claude/pricing
- **Capturada:** 2026-09-15
- **Tipo:** documentación oficial
- **Para qué la usé:** poner el número exacto de la diferencia de costo entre Opus y Sonnet
  antes de recomendar qué modelo va en cada agente de `.claude/agents/`.

## Extracto

> | Claude Opus 5 | $5 / MTok | … | $25 / MTok |
> | Claude Sonnet 5 | $2 / MTok | … | $10 / MTok |
> | Claude Haiku 4.5 | $1 / MTok | … | $5 / MTok |
> | Claude Fable 5.1 | $10 / MTok | … | $50 / MTok |

(Columnas: input base y output. Hay columnas intermedias de escritura de caché que se omiten
acá y se detallan abajo.)

> **Note:** The $2/$10 per million input/output token pricing for Claude Sonnet 5, announced at
> launch as introductory pricing through August 31, 2026, is now the standard price. The
> previously scheduled increase to $3/$15 per million input/output tokens on September 1, 2026
> will not occur.

Sobre los modificadores:

> | 5-minute cache write | 1.25x base input price |
> | 1-hour cache write | 2x base input price |
> | Cache read (hit) | 0.1x base input price |

> Claude 4.6 and later models and Claude Mythos Preview include the full 1M token context window
> at standard pricing. (A 900k-token request is billed at the same per-token rate as a 9k-token
> request.)

> **Claude 4.7 and later models** … use a newer tokenizer that contributes to their improved
> performance on a wide range of tasks. This tokenizer produces **approximately 30% more tokens
> for the same text**.

## Datos que me llevé

- **Opus 5 es exactamente 2,5× Sonnet 5** — en entrada ($5 vs $2) y en salida ($25 vs $10).
  No 5×, que es lo que uno supone.
- Batch API: 50% de descuento (Opus 5 $2,50/$12,50 · Sonnet 5 $1/$5).
- Leer caché cuesta 10% del input; escribirla, 1,25× (5 min) o 2× (1 h). Escribir sale más caro
  que no cachear hasta la primera lectura.
- El 1M de contexto **no** tiene recargo desde 4.6: se paga por token, no por ventana.
- `inference_geo: "us"` aplica 1,1× sobre todo.
- Recomendación textual de Anthropic: *"Choose Haiku for simple tasks, Sonnet for most production
  workloads, and Opus for the most complex reasoning."*

## Qué NO dice / límites

- **No dice nada sobre calidad.** El precio no dice qué modelo escribe mejores tests o mejores
  specs; para eso no hay tabla.
- Los precios y los alias cambian: la nota de Sonnet 5 muestra que una suba anunciada se canceló.
- No publica los límites de rate: los manda a la consola.
- El tokenizer nuevo (≈30% más tokens para el mismo texto) hace que comparar costos de versiones
  distintas por "tokens" engañe; hay que comparar por contenido.
