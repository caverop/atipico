---
name: atipico-local-postgres-tooling
description: "Dónde están las herramientas CLI de PostgreSQL local (no están en el PATH) — sirven para correr migraciones y pg_dump sin pedírselo al usuario. Ojo: la base de dev ya no es la local."
metadata:
  type: reference
---

> Redactado al migrar (2026-08-31): esta carpeta va a git, así que la contraseña del
> superusuario local no se anota acá — pedírsela al usuario si hace falta.
>
> **Estado:** desde 2026-08-19 la base de desarrollo es Neon, no esta
> ([[atipico-neon-deployment]]). El servidor local sigue instalado y corriendo
> (verificado 2026-08-31: servicio `postgresql-x64-18` en Running, `psql.exe` presente),
> así que sigue siendo útil para bases descartables de prueba — pero no se toca
> `restaurante_db` esperando que sea lo que ve la app.

El servidor PostgreSQL 18 local corre como servicio de Windows (`postgresql-x64-18`)
en `localhost:5432`. Superusuario: `postgres` (contraseña no anotada acá). Bases
presentes: `postgres`, `restaurante_db`, `usertask_db`.

`pg_dump`, `psql`, `createdb`, `dropdb` etc. **no están en el PATH**, pero están
instalados en `C:\Program Files\PostgreSQL\18\bin\`. Llamarlos con el path completo:

```
"/c/Program Files/PostgreSQL/18/bin/psql.exe" --host=localhost --port=5432 --username=postgres \
  -d restaurante_db -v ON_ERROR_STOP=1 -f sql/00X_algo.sql
```

**Por qué importa:** antes en este proyecto concluí "psql no está" (había chequeado
solo `where psql`) y le dije al usuario que corriera él las migraciones
`sql/004_*.sql`/`005_*.sql`. Era innecesario — puedo correr migraciones, sacar
snapshots `pg_dump --schema-only` y levantar bases descartables con
`createdb`/`dropdb` usando el path completo. Igual conviene confirmar con el usuario
antes de aplicar una migración a una base real (los cambios de esquema son difíciles
de revertir y son estado compartido), pero no hace falta decir que la herramienta no
está disponible.

Relacionado: [[atipico-improvement-plan]] documenta el drift de `ck_cuenta_metodo`
entre `sql/script_inicial.sql` (que documenta `YAPE`/`PLIN`) y lo realmente desplegado
(`QR`) — confirmado con un `pg_dump` en vivo el 2026-08-18. `sql/schema_completo.sql`
es un snapshot consolidado generado con pg_dump del esquema real.
