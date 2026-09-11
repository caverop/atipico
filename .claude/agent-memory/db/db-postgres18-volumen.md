---
name: db-postgres18-volumen
description: "postgres:18-alpine aborta con exit 1 si el volumen se monta en /var/lib/postgresql/data. La imagen 18+ quiere el volumen en el padre, /var/lib/postgresql, y crea sola un subdirectorio versionado adentro."
metadata:
  type: reference
---

Verificado el 2026-09-10 levantando `docker-compose.db.yml`
(`specs/postgres-local-dev.md` §8.2): montar el volumen en `/var/lib/postgresql/data`
—el punto de montaje que usa casi todo ejemplo de `docker-compose.yml` con Postgres que
circula— hace que el entrypoint de `postgres:18-alpine` **aborte al arrancar**
(`exit 1`). El log es explícito:

> *"there appears to be PostgreSQL data in: /var/lib/postgresql/data (unused
> mount/volume) [...] The suggested container configuration for 18+ is to place a single
> mount at /var/lib/postgresql"*

La imagen 18+ espera el punto de montaje **un nivel arriba**, `/var/lib/postgresql`, y
crea sola un subdirectorio versionado adentro (estilo `pg_ctlcluster`) — así soporta
`pg_upgrade --link` sin que el punto de montaje quede en el medio.

**Aplica a cualquier compose nuevo que use esta imagen**, no solo a
`docker-compose.db.yml`:

```yaml
volumes:
  - mi-volumen:/var/lib/postgresql   # NO .../data
```

Relacionado: [[db-database-tests-catalogo-normalizado]] (mismo `postgres:18-alpine`,
mismo bug, en el contenedor de `Atipico.Database.Tests`).
