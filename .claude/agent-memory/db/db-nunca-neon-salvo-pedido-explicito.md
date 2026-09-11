---
name: db-nunca-neon-salvo-pedido-explicito
description: "No tocás ninguna instancia que persiste y no es tuya —Neon (qa/production) ni tampoco localhost:5433 (dev local del usuario)— salvo que el usuario lo pida explícito en ese momento. Lo que necesites, lo hacés en tu propio contenedor descartable."
metadata:
  type: feedback
---

**Regla final, fijada el 2026-09-10 después de tres correcciones.** La frontera no es
"Neon sí, el resto no" — es **persistencia y propiedad**, no el motor. Cualquier
instancia que **persiste y no es tuya** —Neon (`qa`/`production`), pero también
`localhost:5433`/`docker-compose.db.yml` (la instancia local *del usuario*, `dev`, ver
`db-postgres18-volumen.md` y `specs/postgres-local-dev.md`)— se trata igual: no te
conectás por tu cuenta bajo ningún concepto, **ni siquiera de lectura**, salvo que el
usuario te lo pida explícito, para esa vez puntual. Lo que necesites verificar o generar
por tu cuenta, lo hacés en un **contenedor descartable propio**: nace para eso, muere al
terminar.

**Cómo se llegó acá, con las frases textuales:**

1. Primera versión del día: *"puedes conectar a NEON como solo lectura"* — habilitaba
   `SELECT`/`pg_dump --schema-only` por tu cuenta.
2. Corrección, horas después: *"dev se conecta a base de datos local al igual que tus
   pruebas corren en un docker. no son necesarias credenciales ya que qa y produccion
   corro yo todo manualmente"* — ya apuntaba a que no hacía falta que vos tocaras Neon.
3. Versión "final" del mismo día: *"mis pruebas de desarrollo y tus pruebas todas
   localmente en instancias de docker. los scripts que se deban pasar a qa y prod los
   correre manualmente, te pasare el resultado para que tu pueda verificar que esta todo
   bien, bajo ningun concepto a noser a pedido explicito te conectas a neon"*. En ese
   momento la regla todavía se leía como "Neon no, Docker sí" — porque hablaba solo de
   Neon.
4. **La generalización, horas después, con un incidente de por medio.** Regenerando
   `sql/schema_completo.sql` ya sin tocar Neon, el agente corrió `pg_dump` directo contra
   `localhost:5433` — la instancia local **del usuario**, pensando que "no ser Neon"
   alcanzaba. El usuario lo frenó: *"quedamos que la instancia para validar los test
   corren por tu lado en tu instancia, solo mis pruebas manuales yo voy a ejecutar el
   dump"*. Ahí quedó explícito que la regla nunca fue sobre Neon específicamente — es
   sobre **qué instancia es tuya** (nace y muere por corrida) **y cuál es ajena**
   (persiste, la use quien la use para probar). `localhost:5433` es tan ajena como Neon,
   solo que no está en la nube.

**Por qué se llegó ahí, no es capricho:** el mismo día, verificando `sql/
015_cuenta_metodo_qr.sql`, `dotnet user-secrets list` expuso una cadena de conexión de
Neon completa —contraseña incluida— en la conversación, y un intento posterior de
verificar de solo lectura falló por autenticación (la credencial se había rotado sin
avisarte). Horas después, el mismo patrón se repitió con `localhost:5433` en vez de Neon:
la falla no fue "conectarse a Neon", fue **conectarse a algo que no es tuyo sin que te lo
pidan**. Por eso la regla final no distingue Neon de Docker local, ni lectura de
escritura — es sobre propiedad de la instancia, punto.

**Cómo aplicarlo:**

- El entregable sigue siendo el mismo (script + runbook + consulta de verificación con
  el resultado esperado, `specs/agente-db.md` §2.3) — lo que cambia es quién ejecuta la
  consulta de verificación contra una instancia ajena: **el usuario**, siempre. Vos
  verificás comparando lo que él te pasa contra el resultado esperado que ya escribiste,
  no conectándote a confirmarlo vos mismo.
- "Parece que esta tarea necesita Neon/el dev del usuario" no es un pedido explícito — es
  una inferencia tuya, y no alcanza. Un pedido explícito es el usuario diciéndote en ese
  momento "conectate vos" o dándote la credencial directamente. Ni siquiera vale
  generalizar un pedido explícito de una vez a "entonces puedo seguir haciéndolo" — es
  por esa vez, y para esa instancia puntual.
- **Regenerar `sql/schema_completo.sql` ya no pide ninguna instancia ajena.** Levantás tu
  propio contenedor descartable (`docker run postgres:18-alpine`, distinto puerto del
  `:5433` del usuario), le aplicás `script_inicial.sql` + toda migración numerada en
  orden (mismo patrón que `PostgresFixture` en `Atipico.Database.Tests`), le hacés
  `pg_dump --schema-only --no-owner --no-privileges`, y lo tirás con `docker rm -f`. No
  hay nada "bloqueado" esperando una credencial — nunca la necesitaste para esto.
- Si necesitás extraer una cadena de conexión de `dotnet user-secrets` para OTRO fin (por
  ejemplo, para tu propia instancia de Testcontainers, que no es Neon), capturala directo
  a una variable de shell en el mismo comando que la usa — nunca la imprimas suelta. Pasó
  una vez con un `dotnet user-secrets list` sin capturar, y otra vez con un `sed` que solo
  tapaba una línea y dejó pasar ocho secretos más (R2, JWT, Aspire) sin querer.

Relacionado: [[db-database-tests-catalogo-normalizado]], `specs/postgres-local-dev.md`,
`specs/agente-db.md` §2.2, §2.4, §5.6.
