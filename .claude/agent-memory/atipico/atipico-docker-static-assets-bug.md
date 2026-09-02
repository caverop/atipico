---
name: atipico-docker-static-assets-bug
description: "Causa raíz y fix del 404 de _framework/blazor.web.js en el deploy de Atipico.Web — un patrón de caché de capas Docker que descarta silenciosamente los static assets de Blazor."
metadata:
  type: project
---

`atipico-web.onrender.com` daba 404 en `_framework/blazor.web.js` (y
`blazor.server.js`). Causa raíz encontrada el 2026-08-19 construyendo ambos
Dockerfiles localmente con Docker Desktop y biseccionando los pasos del build.

**Causa raíz:** ambos Dockerfiles usaban la optimización estándar de caché de capas —
copiar solo los `.csproj`, `dotnet restore`, después `COPY . .`, después
`dotnet publish --no-restore`. Para un proyecto cuyos static web assets vienen de un
paquete NuGet y no de archivos locales (los `blazor.web.js`/`blazor.server.js` de
Blazor, que llegan vía `microsoft.aspnetcore.app.internal.assets`), un `dotnet restore`
corrido cuando `wwwroot/` y el resto del proyecto todavía no existen deja `obj/` en un
estado donde el publish posterior con `--no-restore` NO vuelve a descubrir esos
assets. El publish termina con cero warnings y produce una DLL que funciona — solo que
omite `wwwroot/_framework/*` entero. Confirmado con
`docker run --entrypoint sh ... -c "find /app/wwwroot"` sobre la imagen construida.

**Fix (aplicado):** sacar `--no-restore` del `dotnet publish` en ambos Dockerfiles,
manteniendo la capa de restore con los `.csproj` para caché. `dotnet publish`
re-restaura contra el árbol completo (rápido, los paquetes ya están en caché) y
descubre bien los assets. Verificado end-to-end con `docker run`:
`/_framework/blazor.web.js` → 200.

**Cómo verificar un cambio así localmente sin desplegar:**
`docker build --no-cache -f X/Dockerfile -t test .`, después
`docker run --rm --entrypoint sh test -c "find /app/wwwroot"` para inspeccionar el
filesystem de la imagen. No confiar en `docker run test <cmd>` — con un `ENTRYPOINT`
en forma exec, los args extra se le anexan, no lo sustituyen; hay que sobrescribirlo
explícitamente. Después `docker run -d -p PORT:10000 -e
ASPNETCORE_ENVIRONMENT=Production test` y curl al endpoint real.

**Trampa de Windows/Git Bash encontrada en el camino:** la conversión de paths de
MSYS rompe los args tipo `/app/wwwroot` (los convierte a path de Windows). Prefijar
con `MSYS_NO_PATHCONV=1` al pasar paths POSIX a `docker exec`/`docker run`.

Este hallazgo quedó documentado como decisión en `specs/cicd-github-azure-render.md` §2,
que advierte contra `--no-restore` como "una mina ya pisada".

Relacionado: [[atipico-docker-port-zombie-gotcha]].
