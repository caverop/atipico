---
name: atipico-browsh-tty
description: Navegadores de terminal instalados (browsh nativo, carbonyl vía Docker) y accesibles y usables por el agente. Corregido el 2026-09-16 — antes se creía que el shell sin TTY los bloqueaba.
metadata:
  type: reference
---

Instalado el 2026-09-15 a pedido del usuario: `winget install browsh.browsh` trajo
`browsh 1.8.2` y, como dependencia, `Mozilla.Firefox 156.0`.

Dónde vive cada cosa:

- `browsh.exe` → `C:\Users\pdieg\AppData\Local\Microsoft\WinGet\Packages\browsh.browsh_Microsoft.Winget.Source_8wekyb3d8bbwe\`
  (winget ya lo puso en el PATH de usuario).
- `firefox.exe` → `C:\Program Files\Mozilla Firefox\`. El default de browsh para
  `--firefox.path` es el literal `"firefox"`, que no resolvía; agregué esa carpeta al
  PATH de usuario para que `browsh` arranque sin flags.

**Corregido el 2026-09-16, por el usuario: sí puedo acceder y usar tanto browsh como
carbonyl.** La entrada original (2026-09-15) asumía que el shell de las tools, corriendo
`-NonInteractive` con stdin en el dispositivo nulo, bloqueaba cualquier cliente que
dibuje sobre un TTY real — el usuario corrigió eso explícitamente, así que la asunción de
"0 bytes de salida y sin error" ya no se toma como bloqueo general. Sigue valiendo
[[atipico-user-tests-himself]] para las pruebas de navegador de Atipico en sí (las hace el
usuario); esto es sobre poder manejar estos clientes de terminal, no sobre quién hace el
QA de la app. Playwright (MCP) sigue disponible como alternativa cuando corresponda.

Trampa de PowerShell 5.1 que costó una corrida: `Start-Process -ArgumentList` con un
array parte las rutas con espacios (`C:\Program` / `Files\...`). Va la línea de
argumentos como un solo string con las comillas puestas a mano.

## Carbonyl (2026-09-15)

**No existe nativo en Windows**: no está en winget y el proyecto solo publica binarios
para Linux y macOS. WSL tampoco es opción directa acá — la única distro registrada es
`docker-desktop`, que no es un Linux de propósito general. Corre por Docker, con la
imagen `fathyb/carbonyl:latest` (405 MB, amd64+arm64, **sin actualizar desde febrero de
2023** — el proyecto está dormido; browsh está más vivo).

Le dejé un shim en `C:\Users\pdieg\.local\bin\carbonyl.cmd` (esa carpeta ya estaba en el
PATH, junto a `graphify.exe`) que envuelve `docker run --rm -ti fathyb/carbonyl %*`.
Requiere Docker Desktop andando; vale la misma limitación de TTY que browsh.

Dato verificado ese día: **arrancar Docker Desktop no levanta solo el contenedor
`atipico-db-1`** — quedó en `Exited`. Hay que subirlo a mano con `docker-compose.db.yml`.
Igual sigue valiendo [[atipico-runbook-y-verificacion]]: esa instancia es del usuario y no
me conecto a ella.
