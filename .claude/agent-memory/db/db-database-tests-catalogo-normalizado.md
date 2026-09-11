---
name: db-database-tests-catalogo-normalizado
description: "Trampas reales al comparar catálogo de Postgres entre dos bases (Atipico.Database.Tests): \restrict de psql, IN vs ANY(ARRAY), CRLF en dollar-quoting, líneas en blanco de pg_dump. specs/agente-db.md §5.6."
metadata:
  type: reference
---

`Atipico.Database.Tests` (implementado 2026-09-10, `specs/agente-db.md` §4/§5.6) compara
catálogo de dos bases Postgres construidas distinto — la cadena canónica
(`script_inicial.sql` + migraciones) contra `schema_completo.sql`. `pg_get_constraintdef`
**no alcanza solo** para que esa comparación sea confiable; hicieron falta cuatro capas de
normalización, cada una encontrada corriendo la suite, no leyéndola:

1. **`\restrict`/`\unrestrict`.** `psql`/`pg_dump` modernos envuelven el volcado entero en
   estos meta-comandos de *cliente* — no son SQL. Cualquier driver que hable el protocolo
   de cable directo (Npgsql, o cualquier cosa que no sea `psql -f`) revienta con
   `syntax error at or near "\"` al toparlos. Filtrar toda línea que empiece con `\` antes
   de ejecutar un `.sql` que pueda venir de `pg_dump`.
2. **`IN (...)` vs `= ANY (ARRAY[...])` no es solo cosmética de versión de `pg_dump`** —es
   estructural: Postgres guarda el árbol de expresión tal como se escribió el DDL
   original y no las colapsa. Migraciones a mano usan `IN (...)`; `pg_dump` siempre usa
   `= ANY (ARRAY[...])`, con un cast por elemento y un paréntesis de más que la forma de
   `IN` no tiene. Sin normalizar, TODA restricción de lista falla por sintaxis y ahoga la
   diferencia real en ruido.
3. **CRLF dentro de `$function$...$function$`.** En un checkout de Windows, si el archivo
   `.sql` tiene CRLF, Postgres guarda el `\r` **literal** dentro de un cuerpo con
   dollar-quoting — no lo trata como fin de línea. El mismo cuerpo volcado por `pg_dump`
   en Linux no lo tiene: mismo código, bytes distintos. Se detecta con `file` (marca
   `"with CRLF, LF line terminators"` de un lado, `"ASCII text"` del otro) o `diff` sin
   más pistas visibles. Normalizar `\r\n`→`\n` en el texto ANTES de ejecutarlo evita que
   el CRLF llegue a guardarse.
4. **Líneas en blanco entre sentencias.** `schema_completo.sql` puede traer una línea
   vacía entre cada sentencia de un trigger que la cadena, tal como está hoy en el repo,
   ya no tiene — semánticamente inerte en PL/pgSQL. Se resuelve colapsando todo run de
   espacio en blanco a uno solo, **después** de sacar los literales de string (para no
   tocar el contenido de un mensaje de `RAISE EXCEPTION`).

**Cómo depurar esto la próxima vez:** no razonarlo, reconstruir las dos bases a mano con
`psql` (o `docker-compose.db.yml` si está arriba) contra dos bases descartables, volcar
`pg_get_functiondef`/`pg_get_constraintdef` de cada lado a un archivo, y `diff`+`cat -A`
(revela CR invisible que un editor normal no muestra).

**Con las cuatro capas, `CadenaVsSnapshotTests` deja de fallar por ruido**: hoy señala
**dos** diferencias reales, no las tres que predecía `specs/agente-db.md` §5.3 —
`fn_pedido_mesa_ocupada`/`tg_pedido_mesa_ocupada` (dropeados por `013`, ausentes de la
cadena, presentes en el snapshot desactualizado) y `ck_usuario_rol` sin `DELIVERY`
(`014`). La tercera, `ck_cuenta_metodo`, ya no aparece — `015` la cerró.

**Otros dos, de armado con xUnit/Testcontainers, no del dominio:**

- Armar las cadenas de conexión (`NpgsqlConnectionStringBuilder`) **antes** de cualquier
  método que las use — un orden invertido da `InvalidOperationException: The
  ConnectionString property has not been initialized` sin pista de cuál cadena está vacía.
- **xUnit v2.9.x no tiene `Assert.Skip`/`SkipUnless` dinámico** (es de v3). El mecanismo
  real: un `FactAttribute`/`TheoryAttribute` propio que setea `Skip` en su constructor —
  se ejecuta en tiempo de *descubrimiento*, antes de correr nada.

Relacionado: [[db-postgres18-volumen]], `specs/agente-db.md` §5.6.
