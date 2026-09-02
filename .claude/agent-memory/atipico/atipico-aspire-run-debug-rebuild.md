---
name: atipico-aspire-run-debug-rebuild
description: "Con aspire run levantado hay que compilar en -c Release, pero Aspire sirve desde bin\Debug: el cambio no se ve hasta hacer Rebuild (no Reiniciar) en el dashboard."
metadata:
  type: project
---

**Compilar en `-c Release` no actualiza lo que sirve `aspire run`.** Las dos reglas son
ciertas a la vez y se contradicen en la práctica: con la app levantada hay que usar
`-c Release` porque `bin\Debug` está bloqueado ([[atipico-locked-bin-release-build]]),
pero Aspire sirve **desde `bin\Debug`**. Resultado: los tests pasan en verde y el
navegador sigue mostrando el código viejo.

**Why:** pasó el 2026-09-01 con el cambio de `ATIPICO2` → `ATIPICO` en el login. El
cambio era correcto, la prueba de bUnit estaba en verde, y la pantalla seguía mostrando lo
anterior. El diagnóstico fue comparar marcas de tiempo: la DLL de `bin\Debug` era 12
minutos anterior al `.razor`. No hay hot reload activo en este proyecto — verificado
buscando en la salida de `aspire run`.

**How to apply:** en el dashboard, menú **Acciones** del recurso → **Rebuild**, no
*Reiniciar*: reiniciar relanza el mismo binario viejo. Rebuild recompila en Debug y
reinicia solo ese recurso, así que se puede reconstruir `atipico-web` sin tocar
`atipico-api`. Confirmar con la marca de tiempo de la DLL antes de dar por buena una
verificación de pantalla.

El dashboard sale con token en la URL (`/login?t=<hex>`), impreso en la salida de
`aspire run`. Los 6 parámetros del AppHost (`db-connection-string`, `jwt-key`, los cuatro
de R2) ya están en sus user secrets, así que `aspire run` no pide nada por consola.
