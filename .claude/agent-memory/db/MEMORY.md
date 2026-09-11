# Memory Index (agente `db`)

## Cómo trabaja el usuario
- [Nunca Neon, salvo pedido explícito](db-nunca-neon-salvo-pedido-explicito.md) — bajo ningún concepto te conectás a Neon por tu cuenta, ni de lectura; el usuario corre ahí y te pasa el resultado.

## Entorno y herramientas
- [Testcontainers: normalizar el catálogo](db-database-tests-catalogo-normalizado.md) — `\restrict`, `IN` vs `ANY(ARRAY)`, CRLF en dollar-quoting, líneas en blanco de `pg_dump`: cuatro capas antes de que comparar dos bases sea confiable.
- [postgres:18-alpine y el punto de montaje](db-postgres18-volumen.md) — el volumen va en `/var/lib/postgresql`, no en `.../data`, o el contenedor aborta con `exit 1`.
