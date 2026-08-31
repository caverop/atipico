---
name: atipico-docker-port-zombie-gotcha
description: "Un proceso dotnet zombi en 127.0.0.1:<puerto> puede tapar silenciosamente a un contenedor Docker mapeado al mismo puerto en Windows — curl le pega al zombi, no al contenedor."
metadata:
  type: reference
---

En Windows con Docker Desktop, si queda corriendo un `dotnet run`/`dotnet Foo.dll`
bindeado a `127.0.0.1:<puerto>` (loopback específico), y después un contenedor recibe
`-p <puerto>:...` al MISMO número de puerto, los requests a
`http://localhost:<puerto>` pueden ser servidos por el **proceso dotnet zombi**, no
por el contenedor — aunque `docker port` y `docker ps` muestren correctamente que el
contenedor tiene ese puerto. Windows prioriza un listener de loopback específico
(`127.0.0.1:X` / `[::1]:X`) por encima de uno wildcard (`0.0.0.0:X` / `[::]:X`, que es
como bindea el proxy de Docker, `com.docker.backend.exe`).

**Síntoma:** el contenedor responde contenido completamente equivocado (p. ej. el HTML
de Blazor de la Web cuando se está curleando lo que debería ser el JSON de la Api),
sin errores en ningún lado, y `docker logs` del contenedor se ve normal — porque el
contenedor está bien, el tráfico nunca le llegó.

**Cómo diagnosticar:** `netstat -ano | grep :<puerto>` y mirar los PID en LISTENING.
Si hay un `dotnet.exe` (o cualquier proceso no-Docker) en
`127.0.0.1:<puerto>`/`[::1]:<puerto>` junto a `com.docker.backend.exe` en
`0.0.0.0:<puerto>`, gana el local. `tasklist //FI "PID eq <pid>"` confirma qué es.
Matarlo y reintentar.

**Cómo evitarlo:** parar del todo los servidores `dotnet run` de prueba (verificar con
`tasklist` después del `taskkill`, no asumir que funcionó) antes de reusar el mismo
puerto para un contenedor — sobre todo en sesiones largas que usaron muchos puertos
ad-hoc (15000-15004) para verificaciones puntuales.

Hay un `docker-compose.yml` + `.env.example` en la raíz para probar `atipico-api` y
`atipico-web` juntos (puertos 15000/15001, red interna `web`→`api` por el nombre de
servicio de compose).

Relacionado: [[atipico-docker-static-assets-bug]], [[atipico-locked-bin-release-build]].
