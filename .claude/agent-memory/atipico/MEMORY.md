# Memory Index

## Cómo trabaja el usuario
- [Spec primero](atipico-spec-primero.md) — spec → plan → aprobación → código; formato de `specs/<feature>.md` y las frases con que lo pide.
- [Él hace las pruebas](atipico-user-tests-himself.md) — verificar con build, no levantar servidores ni Playwright salvo pedido.
- [Bin bloqueado → Release](atipico-locked-bin-release-build.md) — con la API corriendo, `-c Release`; nunca matarle el proceso.
- [No editar .claude/settings.json](atipico-dont-edit-claude-settings.md) — rechazó la edición aun con contraseñas filtradas; reportar, no tocar.

## Decisiones de diseño que se hacen cumplir
- [Timestamps del servidor](atipico-server-stamped-timestamps.md) — las fechas de transición se estampan en el servidor, nunca las tipea el usuario.
- [Angostar enum, no migrar](atipico-narrow-enum-over-migration.md) — si el CHECK ya permite un superconjunto, solo se toca el enum de C#.

## Historia del proyecto
- [Plan de mejoras (completado)](atipico-improvement-plan.md) — el radio de impacto de `app_restaurante`, los botones "Quitar" que ya no pueden funcionar, y por qué se dejaron así.
- [azd provision pisa el portal](atipico-azd-provision-pisa-portal.md) — lo que no está en `AppHost.cs` se borra en cada deploy; `azd deploy` no, solo `provision`.
- [Despliegue en Neon](atipico-neon-deployment.md) — la base de dev es Neon; Npgsql no parsea URIs `postgres://`; datos de prueba sembrados.
- [Bug de static assets en Docker](atipico-docker-static-assets-bug.md) — `--no-restore` descarta los `_framework/*` de Blazor sin un solo warning.

## Pendientes
- [Los pendientes van en TODO.md](atipico-todo-netarchtest.md) — en la raíz, no en memoria; hoy el único es evaluar NetArchTest.

## Trampas del framework
- [Comentarios Razor que rompen](atipico-razor-comentario-en-atributos.md) — entre atributos de un componente compilan y revientan en runtime; y `*@` adentro del comentario lo corta.

## Entorno y herramientas
- [PostgreSQL local](atipico-local-postgres-tooling.md) — los binarios están en `C:\Program Files\PostgreSQL\18\bin\`, fuera del PATH; ojo, ya no es la base de dev.
- [aspire run sirve Debug](atipico-aspire-run-debug-rebuild.md) — compilás en Release y la pantalla no cambia; en el dashboard va Rebuild, no Reiniciar.
- [Puerto zombi con Docker](atipico-docker-port-zombie-gotcha.md) — un `dotnet` en `127.0.0.1:<puerto>` tapa al contenedor mapeado al mismo puerto.
- [graphify y Obsidian](atipico-graphify-y-obsidian.md) — graphify no necesita clave (el LLM es el agente); Obsidian ignora carpetas con punto, la memoria no entra al vault.
