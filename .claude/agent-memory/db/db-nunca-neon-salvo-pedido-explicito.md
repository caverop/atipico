---
name: db-nunca-neon-salvo-pedido-explicito
description: "Bajo ningún concepto te conectás a Neon (qa/production) por tu cuenta, ni de lectura, salvo que el usuario lo pida explícito en ese momento. El corrige a mano, te pasa el resultado, vos verificás sobre eso."
metadata:
  type: feedback
---

**Regla final, fijada el 2026-09-10 después de dos correcciones.** Contra Neon (`qa` y
`production` — `dev` ya no es Neon, es Docker local, ver `db-postgres18-volumen.md` y
`specs/postgres-local-dev.md`): no te conectás por tu cuenta bajo ningún concepto, **ni
siquiera de lectura**, salvo que el usuario te lo pida explícito, para esa vez puntual.

**Cómo se llegó acá, con las frases textuales:**

1. Primera versión del día: *"puedes conectar a NEON como solo lectura"* — habilitaba
   `SELECT`/`pg_dump --schema-only` por tu cuenta.
2. Corrección, horas después: *"dev se conecta a base de datos local al igual que tus
   pruebas corren en un docker. no son necesarias credenciales ya que qa y produccion
   corro yo todo manualmente"* — ya apuntaba a que no hacía falta que vos tocaras Neon.
3. Versión final, más precisa: *"mis pruebas de desarrollo y tus pruebas todas
   localmente en instancias de docker. los scripts que se deban pasar a qa y prod los
   correre manualmente, te pasare el resultado para que tu pueda verificar que esta todo
   bien, bajo ningun concepto a noser a pedido explicito te conectas a neon"*.

**Por qué se llegó ahí, no es capricho:** el mismo día, verificando `sql/
015_cuenta_metodo_qr.sql`, `dotnet user-secrets list` expuso una cadena de conexión de
Neon completa —contraseña incluida— en la conversación, y un intento posterior de
verificar de solo lectura falló por autenticación (la credencial se había rotado sin
avisarte). Los dos incidentes tienen la misma causa raíz: **la existencia misma de una
conexión tuya a Neon**, no qué operación específica intentabas hacer ahí. Por eso la
regla final no distingue lectura de escritura — las elimina las dos.

**Cómo aplicarlo:**

- El entregable sigue siendo el mismo (script + runbook + consulta de verificación con
  el resultado esperado, `specs/agente-db.md` §2.3) — lo que cambia es quién ejecuta la
  consulta de verificación contra Neon: **el usuario**, siempre. Vos verificás
  comparando lo que él te pasa contra el resultado esperado que ya escribiste, no
  conectándote a confirmarlo vos mismo.
- "Parece que esta tarea necesita Neon" no es un pedido explícito — es una inferencia
  tuya, y no alcanza. Un pedido explícito es el usuario diciéndote en ese momento
  "conectate vos" o dándote la credencial directamente. Ni siquiera vale generalizar un
  pedido explícito de una vez a "entonces puedo seguir haciéndolo" — es por esa vez.
- Regenerar `sql/schema_completo.sql` (pide `pg_dump` contra Neon, `specs/agente-db.md`
  §2.3/§2.4) entra de lleno acá: es un paso del usuario, no algo que quede "bloqueado"
  esperando que vos consigas una credencial. Dejá de pedirla.
- Si necesitás extraer una cadena de conexión de `dotnet user-secrets` para OTRO fin (por
  ejemplo, para tu propia instancia de Testcontainers, que no es Neon), capturala directo
  a una variable de shell en el mismo comando que la usa — nunca la imprimas suelta. Pasó
  una vez con un `dotnet user-secrets list` sin capturar, y otra vez con un `sed` que solo
  tapaba una línea y dejó pasar ocho secretos más (R2, JWT, Aspire) sin querer.

Relacionado: [[db-database-tests-catalogo-normalizado]], `specs/postgres-local-dev.md`,
`specs/agente-db.md` §2.2.
