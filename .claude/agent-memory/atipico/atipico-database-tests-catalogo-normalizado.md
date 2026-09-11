---
name: atipico-database-tests-catalogo-normalizado
description: "Movido a la memoria del agente db — es quien interactúa con la base. Puntero corto: las trampas de comparar catálogo de Postgres (Atipico.Database.Tests) están en .claude/agent-memory/db/."
metadata:
  type: reference
---

**Esta nota se movió.** El detalle completo —`\restrict` de psql, `IN` vs
`ANY(ARRAY)`, CRLF en dollar-quoting, líneas en blanco de `pg_dump`— vive ahora en
`.claude/agent-memory/db/db-database-tests-catalogo-normalizado.md`: es el agente `db`
quien interactúa con la base y quien va a necesitar esto, corregido el 2026-09-10 a
pedido del usuario.

Relacionado: [[atipico-no-commit-sin-aprobacion]].
