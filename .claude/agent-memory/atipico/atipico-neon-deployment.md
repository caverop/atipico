---
name: atipico-neon-deployment
description: "La base de dev/deploy de Atipico está en Neon — dónde vive la cadena de conexión, el setup de app_restaurante que se hizo ahí, los datos de prueba sembrados, y el gotcha real de Npgsql con URIs."
metadata:
  type: project
---

> Redactado al migrar (2026-08-31): esta carpeta está versionada en git, así que se
> sacaron de esta nota el host completo de Neon, el hash BCrypt y la contraseña de
> prueba. Nada de eso es necesario para trabajar; lo que hace falta está en user
> secrets o se le pide al usuario.

Desde 2026-08-19 el dev local apunta a un proyecto Postgres en **Neon** (base
`atipico-db`, host `ep-<...>-pooler.<región>.aws.neon.tech`), no al Postgres local.
El usuario pidió que la credencial NO se commitee: `ConnectionStrings:DefaultConnection`
en `Atipico.Api/appsettings.Development.json` es string vacío (trackeado, sin secreto,
verificado que sigue así el 2026-08-31) y el valor real vive en .NET User Secrets
(`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..." --project
Atipico.Api`; el `UserSecretsId` está en `Atipico.Api.csproj`) — fuera del repo, por
máquina. Documentado en `CLAUDE.md`.

El esquema en Neon ya coincidía con `sql/schema_completo.sql` (BLOQUE 1) cuando se
encontró — alguien ya lo había corrido. **BLOQUE 2 (el rol `app_restaurante`) faltaba**
y hubo que crearlo igual que en local: `CREATE ROLE app_restaurante LOGIN`, el set
estándar de GRANT/REVOKE, `ALTER DEFAULT PRIVILEGES`, y después
`ALTER ROLE ... WITH PASSWORD`. La app se conecta como `app_restaurante`, no como el
rol dueño del proyecto Neon (`neondb_owner`) — misma postura de mínimo privilegio que
en local, confirmada en vivo (`app_restaurante` puede INSERT; DELETE falla
correctamente con "permission denied").

**Bug real encontrado: Npgsql no parsea cadenas tipo URI**
`postgresql://user:pass@host/db?...`. El dashboard de Neon te da ese formato por
default, pero `NpgsqlConnectionStringBuilder` tira
`KeyNotFoundException`/`ArgumentException` al intentar leerlo como pares
keyword=value de ADO.NET. Hay que convertirlo a
`Host=...;Port=...;Database=...;Username=...;Password=...;SSL Mode=Require;Channel Binding=Require`
— el mismo formato que el resto del repo. Si vuelve a aparecer una cadena
`postgres://`, convertirla antes de que llegue a Npgsql/EF Core.

**Sobre hashes BCrypt recordados:** en una sesión recordé de memoria un hash para la
contraseña de los usuarios de prueba y estaba **mal** (no verificaba). No confiar
nunca en un hash traído de memoria: verificarlo con `BCrypt.Net.BCrypt.Verify` contra
la contraseña real, o leerlo de la fila viva en la DB. El hash correcto está en la
tabla `usuario` de Neon; no se copia acá.

**Datos de prueba sembrados en la `atipico-db` de Neon:** 4 usuarios de prueba
(`test.admin`, `test.mesero`, `test.cajero`, `test.cocinero`, uno por `RolUsuario`,
todos con la misma contraseña compartida — pedírsela al usuario, no está acá),
4 `tipo_plato` (Entradas, Fondos, Postres, Bebidas), 6 `plato`, 4 `mesa` (números 1-4).
Verificado end-to-end: login vía `POST /api/auth/login` como `test.admin` contra la
API local apuntando a Neon, JWT válido, y `/api/platos` + `/api/mesas` respondieron.

Relacionado: [[atipico-local-postgres-tooling]] (el Postgres local sigue instalado,
pero ya no es la base de dev), [[atipico-dont-edit-claude-settings]]. El spec
`specs/cicd-github-azure-render.md` decide que QA y producción comparten la misma base
de Neon, y que los buckets se separan junto con la base y no antes.
