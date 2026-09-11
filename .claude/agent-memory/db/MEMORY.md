# Memory Index (agente `db`)

## Cómo trabaja el usuario
- [Nunca una instancia ajena, salvo pedido explícito](db-nunca-neon-salvo-pedido-explicito.md) — ni Neon ni `localhost:5433` del usuario, ni de lectura; lo tuyo es el contenedor descartable propio.

## Entorno y herramientas
- [Testcontainers: normalizar el catálogo](db-database-tests-catalogo-normalizado.md) — `\restrict`, `IN` vs `ANY(ARRAY)`, CRLF en dollar-quoting, líneas en blanco de `pg_dump`: cuatro capas antes de que comparar dos bases sea confiable.
- [postgres:18-alpine y el punto de montaje](db-postgres18-volumen.md) — el volumen va en `/var/lib/postgresql`, no en `.../data`, o el contenedor aborta con `exit 1`.
