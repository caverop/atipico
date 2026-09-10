# Memory Index

## Cómo trabaja el usuario
- [Spec primero](atipico-spec-primero.md) — spec → plan → aprobación → código; el formato canónico vive en `specs/formato-spec.md`, leerlo antes de escribir uno.
- [Jira corto, spec largo](atipico-jira-corto-spec-largo.md) — el ticket lleva lo esencial y un puntero; el detalle vive en el spec.
- [No commitear sin aprobación](atipico-no-commit-sin-aprobacion.md) — implementar y verificar sí, pero commit/push esperan el visto bueno explícito, aunque el spec ya esté aprobado.
- [El grafo va con el commit](atipico-grafo-con-el-commit.md) — commiteás specs, actualizás el grafo en el mismo turno; un hook de git no puede.
- [Extracción del grafo: Haiku en segundo plano](atipico-grafo-haiku-background.md) — la extracción va a Haiku en background; la orquestación (merge/rebuild/poda/commit) la seguís haciendo vos, en vivo, con los guardas siempre obligatorios.
- [Runbook y verificación](atipico-runbook-y-verificacion.md) — para cualquier script contra una instancia real: entregás el documento de pasos, el usuario ejecuta, vos verificás con las consultas necesarias.
- [Él hace las pruebas](atipico-user-tests-himself.md) — verificar con build, no levantar servidores ni Playwright salvo pedido.
- [Bin bloqueado → Release](atipico-locked-bin-release-build.md) — con la API corriendo, `-c Release`; nunca matarle el proceso.
- [No editar .claude/settings.json](atipico-dont-edit-claude-settings.md) — rechazó la edición aun con contraseñas filtradas; reportar, no tocar.

## Decisiones de diseño que se hacen cumplir
- [Timestamps del servidor](atipico-server-stamped-timestamps.md) — las fechas de transición se estampan en el servidor, nunca las tipea el usuario.
- [Angostar enum, no migrar](atipico-narrow-enum-over-migration.md) — si el CHECK ya permite un superconjunto, solo se toca el enum de C#.

## Historia del proyecto
- [Plan de mejoras (completado)](atipico-improvement-plan.md) — el radio de impacto de `app_restaurante`, los botones "Quitar" que ya no pueden funcionar, y por qué se dejaron así.
- [azd provision pisa el portal](atipico-azd-provision-pisa-portal.md) — lo que no está en `AppHost.cs` se borra en cada deploy; `azd deploy` no, solo `provision`.
- [Despliegue en Neon](atipico-neon-deployment.md) — Npgsql no parsea URIs `postgres://`; datos de prueba sembrados. La base de dev *era* Neon — en transición a local, ver abajo.
- [dev sale de Neon, va a Docker local](atipico-postgres-local-dev.md) — SCRUM-29, `implementado`: Docker persistente en 5433, levantado y poblado. qa/production no cambian.
- [Bug de static assets en Docker](atipico-docker-static-assets-bug.md) — `--no-restore` descarta los `_framework/*` de Blazor sin un solo warning.

## Pendientes
- [Los pendientes van en TODO.md](atipico-todo-netarchtest.md) — en la raíz, no en memoria; hoy el único es evaluar NetArchTest.

## Trampas del framework
- [Comentarios Razor que rompen](atipico-razor-comentario-en-atributos.md) — entre atributos de un componente compilan y revientan en runtime; y `*@` adentro del comentario lo corta.

## Entorno y herramientas
- [Atipico.Database.Tests: normalizar el catálogo](atipico-database-tests-catalogo-normalizado.md) — `\restrict`, `IN` vs `ANY(ARRAY)`, CRLF en dollar-quoting, líneas en blanco de `pg_dump`: cuatro capas antes de que la comparación sea confiable.
- [PostgreSQL local](atipico-local-postgres-tooling.md) — el nativo en `C:\Program Files\PostgreSQL\18\bin\` nunca es la base de dev; desde 2026-09-10 esa es Docker en 5433, no Neon.
- [aspire run sirve Debug](atipico-aspire-run-debug-rebuild.md) — compilás en Release y la pantalla no cambia; en el dashboard va Rebuild, no Reiniciar.
- [Puerto zombi con Docker](atipico-docker-port-zombie-gotcha.md) — un `dotnet` en `127.0.0.1:<puerto>` tapa al contenedor mapeado al mismo puerto.
- [graphify y Obsidian](atipico-graphify-y-obsidian.md) — graphify no necesita clave (el LLM es el agente); Obsidian ignora carpetas con punto, la memoria no entra al vault.
