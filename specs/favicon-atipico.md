# Reemplazar el favicon de la plantilla de Blazor

Especificación para SCRUM-14 ("Quitar logo Blazor de pagina").

- **Estado:** implementado.
- **Alcance:** el ícono de la pestaña del navegador (`favicon.png`) y su referencia en
  `App.razor`.
- **Fuera de alcance:** cualquier otra marca visual — `NavMenu.razor` ya tiene la suya
  propia y no se toca; no se genera un set de íconos para PWA/manifest porque la app no
  tiene manifest hoy.

---

## 1. Problema

`Atipico.Web/wwwroot/favicon.png` es el ícono por defecto de la plantilla `dotnet new
blazor` (un sobre morado con `@`) — nadie lo reemplazó por una marca propia. Es lo único
"Blazor" que queda en el proyecto: `App.razor` referencia solo este archivo (`grep`
verificado, una sola coincidencia), y no hay ningún otro rastro de branding de plantilla en
`Atipico.Web`.

`NavMenu.razor` ya tiene una marca propia: un hexágono en SVG (`class="atipico-logo"`)
junto al texto `ATIPICO`, en el color `var(--atipico-cream)` sobre el fondo oscuro del
sidebar. SCRUM-14 no traía descripción — la decisión de qué reemplaza al favicon se tomó
en el chat: reutilizar ese mismo hexágono, no diseñar un ícono nuevo.

## 2. Decisión

Nuevo `favicon.svg` con el mismo polígono hexagonal de `NavMenu.razor`, en formato SVG (no
PNG) — los navegadores modernos soportan `<link rel="icon" type="image/svg+xml">`, y así no
hace falta rasterizar ni generar múltiples resoluciones.

### 2.1 El color no puede ser `currentColor`

El SVG original usa `stroke="currentColor"` porque hereda el color de texto de
`.atipico-logo` (`var(--atipico-cream)`, pensado para el fondo oscuro del sidebar). Un
favicon no tiene ese contexto — ningún elemento padre le da color. Se fijó el stroke a
`#1c1712` (`--atipico-ink`, la tinta oscura de la marca) en vez del crema: una pestaña de
navegador es casi siempre clara, y un trazo oscuro es el que se ve ahí. El mismo hexágono,
dos colores distintos según el fondo — no es una inconsistencia, es la misma decisión que ya
existe entre sidebar (oscuro → texto claro) y el resto de la app (clara → texto oscuro,
`--atipico-ink` en `html, body`).

### 2.2 Se mantiene la geometría exacta

`viewBox="0 0 24 24"`, `points="12,1 22,7 22,17 12,23 2,17 2,7"`, `stroke-width="1.5"` —
idénticos a `NavMenu.razor`. Se consideró engrosar el trazo para que se lea mejor a 16px
(tamaño típico de pestaña), pero se descartó: la consigna era reutilizar el ícono existente,
no crear una variante. Si en el uso real se ve demasiado fino, es un ajuste de una sola
línea (`stroke-width`) para más adelante — no bloquea esto.

## 3. Cambios

- **Nuevo:** `Atipico.Web/wwwroot/favicon.svg`.
- **`Atipico.Web/Components/App.razor`** — línea 13:
  ```razor
  - <link rel="icon" type="image/png" href="favicon.png" />
  + <link rel="icon" type="image/svg+xml" href="favicon.svg" />
  ```
- **Borrado:** `Atipico.Web/wwwroot/favicon.png` (sin más referencias, confirmado por
  `grep` antes de borrarlo).

## 4. Verificación

- [x] `grep -rn favicon Atipico.Web/Components` antes del cambio: una sola coincidencia
  (`App.razor:13`) — no hay otro lugar que romper.
- [x] `dotnet build Atipico.Web -c Release` — compilación correcta, 0 advertencias, 0
  errores.
- [ ] Verificación visual en pantalla — la hace el usuario (regla del proyecto: las pruebas
  de navegador las hace el usuario), reiniciando el servidor para que sirva el `App.razor`
  nuevo.

## Bitácora

- **2026-09-05** — SCRUM-14 existía en Jira con estado "En curso" pero sin descripción ni
  comentarios: no había ninguna decisión previa registrada sobre qué reemplaza al ícono.
  Se preguntó directamente y se optó por reutilizar el hexágono de `NavMenu.razor` en vez de
  pasar un archivo propio o dejar la app sin ícono.
