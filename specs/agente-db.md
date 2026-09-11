# Agente `db` — dueño de la base de datos

Especificación para un subagente especializado en base de datos, y para el proyecto de pruebas
`Atipico.Database.Tests` que le sirve de herramienta. Documento previo a la implementación:
recoge las decisiones tomadas, las descartadas y su porqué, más la bitácora de lo verificado
empíricamente el 2026-09-09.

- **Estado:** **`Atipico.Database.Tests` implementado el 2026-09-10** (los tres tests de
  deriva de §4.3 + el de `app_restaurante` sin `DELETE` de §4.2 — las pruebas de trigger
  quedan para después, según §3 punto 5). Referenciado en `Atipico.slnx`, 16 tests: 13 en
  verde, 3 en rojo por la razón correcta (§5.6). Sin Docker, la suite entera se saltea —
  verificado, no falla. **Regenerar `schema_completo.sql` es tarea del usuario, no del
  agente**: pide `pg_dump` contra Neon (§2.3), y el agente no se conecta ahí (§2.2) — ver
  §5.6.
- **Origen:** [SCRUM-19](https://caverop.atlassian.net/browse/SCRUM-19), tipo Task, sin
  descripción ni criterios de aceptación en el ticket — el alcance de este documento es la
  interpretación de lo pedido, acordada en conversación el 2026-09-09.
- **Alcance:** la definición del agente `db`, su frontera con `qa`/`dev`/`atipico`, y el
  diseño del proyecto de pruebas de esquema contra PostgreSQL real.
- **Fuera de alcance:** el monitoreo de corrupción de datos vivos (§6.4); reparar el drift de
  `ck_cuenta_metodo` (§5.4) — eso es una migración con su propia aprobación, no parte de este
  spec.

---

## 1. Problema

Hay trabajo de base de datos real, recurrente y sin dueño. Al 2026-09-09 `sql/` tiene 14
scripts numerados escritos a mano (no hay migraciones EF), 6 scripts `dev_*` de datos, y un
snapshot generado. Sobre eso pesan tres obligaciones que hoy sostiene la disciplina y nada más:

1. **El snapshot se atrasa.** `sql/schema_completo.sql` tiene que representar el resultado de
   correr la cadena completa. Nadie verifica que lo haga, y hoy no lo hace (§5.3).
2. **Los `CHECK` duplican los enums de C# a mano.** `sql/script_inicial.sql` lo dice en su
   propio encabezado de mantenimiento: *"No hay una única fuente de verdad: agregar un valor al
   enum de C# sin actualizar el `CHECK` correspondiente … rompe en producción recién cuando se
   use ese valor nuevo, no al desplegar."*
3. **Las reglas de negocio viven en triggers que nunca se probaron.** `fn_cuenta_inmutable`,
   `fn_detalle_inmutable`, `fn_pedido_plato_facturado`, el índice parcial de comensal único: la
   aplicación los espeja en la UI, pero ningún test confirma que el trigger haga lo que el spec
   dice que hace.

Y hay una obligación de la que ni siquiera se habla: **el rol `app_restaurante` no tiene
`DELETE`**, y de eso depende media arquitectura ("se anula, no se borra"). Es una afirmación en
`CLAUDE.md` que nunca nadie ejecutó contra una base para comprobarla.

## 2. Decisión: un agente `db`, con frontera explícita

### 2.1 Por qué hace falta la frontera

`qa` y `dev` están partidos por **fase** (uno escribe las pruebas antes del código, el otro
implementa contra ellas). Un agente `db` corta por **capa**, y los cruza: si no se define,
quedan dos agentes editando `sql/` con criterios distintos.

La frontera es:

> **`db` es dueño de `sql/` y de la coherencia esquema ↔ C#. En el trabajo de feature es
> consultor, no implementador.**

En concreto:

| Trabajo | Quién |
|---|---|
| Escribir/revisar un script en `sql/` | `db` |
| Regenerar `sql/schema_completo.sql` | `db` |
| Auditar deriva enum ↔ `CHECK` | `db` |
| `Atipico.Database.Tests` (pruebas de esquema) | `db` |
| Pruebas de una feature derivadas de su spec | `qa`, como hasta ahora |
| Implementar la feature en C# | `dev`, como hasta ahora |
| Decidir el alcance de una feature | el spec, aprobado por el usuario |

Cuando una feature necesita una columna nueva, `db` escribe la migración; `dev` implementa el
C# contra ella. No al revés, y no los dos.

**`db` sigue el mismo orden que todos: spec → plan → aprobación → código.** Una migración es un
cambio; pasa por `specs/` antes que por `sql/`.

### 2.2 Regla dura: el agente no se conecta a Neon, ni de lectura

**Actualización 2026-09-10** (`specs/postgres-local-dev.md`, SCRUM-29): `dev` ya no es
Neon — es un `postgres:18-alpine` local (`docker-compose.db.yml`). "La base compartida de
Neon" de acá en más son solo `qa` y `production`.

**Versión final, tras dos correcciones el mismo día** (detalle en
`.claude/agent-memory/db/db-nunca-neon-salvo-pedido-explicito.md`): el agente **no se
conecta a Neon bajo ningún concepto, ni siquiera de lectura, salvo pedido explícito del
usuario en ese momento**. El agente:

- **Nunca** ejecuta DDL ni DML contra Neon. Ni `INSERT`, ni `ALTER`, ni `CREATE`. Nunca.
- **Tampoco lee** — ni `SELECT` ni `pg_dump --schema-only` — por su cuenta. La excepción
  de solo lectura que este párrafo tuvo unas horas se retiró: el usuario decidió que
  ni siquiera eso vale sin que él lo pida en el momento.
- Todo lo que valida, lo valida en un **contenedor descartable** que él mismo levanta, o
  en la instancia local del usuario (`localhost:5433`).
- **El usuario ejecuta** contra Neon, a mano, siempre — y le pasa el resultado al agente
  para que verifique sobre eso, no conectándose él.

Esto no es una precaución genérica: el drift documentado en §5.4 entró exactamente por la vía
de aplicar algo a la base sin que quedara registrado como migración.

### 2.3 El entregable por migración son tres cosas, no una

El punto débil de "el usuario corre los scripts a mano" es que *a mano* se convierte en
*improvisado*, y así entra la deriva. Por eso el entregable del agente para cada migración es:

1. **El script**, `sql/NNN_*.sql`, con el patrón de la casa: envuelto en transacción,
   comentado con el porqué y el ticket, referenciando su spec.
2. **Un runbook**: los comandos exactos, en orden, con la conexión que corresponde — ya
   probados por el agente contra un contenedor limpio, no redactados de memoria.
3. **Una consulta de verificación posterior**: un `SELECT` que el usuario corre *después* de
   aplicar y que confirma que la base quedó donde el script dice. Con el resultado esperado
   escrito al lado.

Así "manual" significa *el usuario ejecuta*, no *el usuario improvisa*.

Y recién **después** de que el usuario confirmó que aplicó, el agente regenera
`sql/schema_completo.sql` desde Neon por `pg_dump`. Ese es el único momento en que toca Neon, y
es de solo lectura.

### 2.4 Regenerar, nunca editar a mano

`sql/schema_completo.sql` es salida literal de `pg_dump --schema-only`; su propio encabezado y
`specs/script-inicial-completo.md` §2.1 lo establecen. Editarlo a mano rompe justamente lo que
ese diseño protege: la próxima regeneración pisa la edición sin que nadie se entere.

**Pero regenerar tiene un riesgo propio**, y hay que nombrarlo: `pg_dump` captura *lo que hay
en Neon*, incluido cualquier cambio aplicado a mano que nunca se escribió como migración. El
snapshot puede **absorber deriva en silencio**. Por eso el test de §4.3.1 no es un lujo: es lo
que convierte "regenerar" en algo verificable.

**Y cuando cadena y snapshot difieran, cuál de los dos tiene razón es decisión del usuario, no
del agente.** El agente reporta la diferencia y propone; no elige. Hay precedente de cómo se
decide: el encabezado de `schema_completo.sql` ya resolvió un caso así escribiendo *"Este
archivo manda"* (§5.4).

## 3. Alcance v1

1. Dueño de `sql/`, de las migraciones numeradas y de sus runbooks. Nunca escribe en Neon.
2. Regenera `sql/schema_completo.sql` después de que el usuario aplica. **Primera tarea:
   ponerlo al día con `013` y `014`** (§5.3).
3. Propone la migración `015` que repara el drift de `ck_cuenta_metodo` (§5.4), con aprobación
   del usuario. No la aplica.
4. Audita la deriva enum ↔ `CHECK`. **Esto no necesita base**: es lectura estática de
   `Atipico.Domain/Enums/` contra `sql/`, y sirve desde el primer día.
5. Monta `Atipico.Database.Tests` (§4): los tres tests de deriva primero, los de trigger
   después.

## 4. `Atipico.Database.Tests`

### 4.1 Testcontainers, no un servicio en `docker-compose.yml`

`docker-compose.yml` hoy tiene `api` y `web` contra una base externa; **no tiene servicio de
base**, y sigue sin tenerlo — el que existe para eso es `docker-compose.db.yml`
(`specs/postgres-local-dev.md`), un archivo aparte para la instancia *persistente del
usuario*, no para esto. Para las verificaciones puntuales del agente la elección sigue
siendo libre, y va por Testcontainers:

- Con compose, el contenedor es **estado que el desarrollador tiene que recordar**. Uno viejo
  conserva datos de la corrida anterior y convierte una prueba de integridad en un *flake*.
- El puerto fijo ya mordió antes: ver `atipico-docker-port-zombie-gotcha` en la memoria del
  agente `atipico`. Testcontainers usa puerto efímero.
- Con Testcontainers el test es dueño del ciclo de vida: CI corre igual que la máquina del
  desarrollador, sin un paso previo que alguien se olvida.

**Un contenedor por colección de tests**, no por test: el arranque medido es ~4s (§5.1), lo que
es barato una vez y caro cuarenta veces.

### 4.2 La fixture abre dos conexiones

En el contenedor el agente es superusuario, cosa que contra Neon nunca fue cierta. Eso habilita
lo que hoy no se puede probar en ningún lado:

- **Conexión de dueño** (`postgres`): monta el esquema, siembra, y limpia al final. Puede
  borrar, así que el *teardown* funciona.
- **Conexión `app_restaurante`**: corre el `BLOQUE 2` de `script_inicial.sql` para crear el rol
  con sus `GRANT` reales, y **las assertions se hacen desde acá**.

Con eso se prueba por primera vez lo que `CLAUDE.md` afirma: que `app_restaurante` **no puede
borrar**, y que por eso todo `DELETE` de la API falla por diseño.

### 4.3 Los tres tests que pagan la infraestructura

No son los de triggers. Son los de deriva.

#### 4.3.1 Cadena == snapshot

Base A: `script_inicial.sql` + `002…NNN` en orden. Base B: `schema_completo.sql`. Se comparan.
Es la objeción de §2.4 vuelta test: si el snapshot se atrasó, o si absorbió deriva, el test lo
dice. **Hoy fallaría**, que es exactamente lo que se quiere de él (§5.3).

#### 4.3.2 Enum ↔ `CHECK`

Reflexión sobre `Atipico.Domain/Enums/*.cs`, pasando cada valor por el
`UpperSnakeCaseEnumConverter` **real** (no por una copia del algoritmo), contra los valores que
devuelve `pg_constraint` para el `CHECK` correspondiente. La tabla de correspondencia ya está
escrita a mano en el encabezado de `sql/script_inicial.sql` y en el de `schema_completo.sql`.

No unifica la fuente de verdad —siguen siendo dos lugares—, pero **vuelve immergeable la
divergencia**, que es todo lo que hace falta.

**Ojo, la relación no es igualdad.** `MetodoPago` define `Efectivo`/`Qr` mientras el `CHECK`
permite cuatro valores, y eso es holgura deliberada y documentada. La assertion correcta es
**subconjunto**: todo valor del enum tiene que estar permitido por el `CHECK`. Un valor de más
en la base es holgura; uno de menos es una ruptura en producción.

#### 4.3.3 Modelo EF ↔ esquema real

`AppDbContext` contra el contenedor. Atrapa una `IEntityTypeConfiguration` que nombre una
columna que no existe, o un tipo que no mapea — hoy eso se descubre en tiempo de ejecución.

Los tests de inmutabilidad (`fn_cuenta_inmutable`, `fn_detalle_inmutable`,
`fn_pedido_plato_facturado`, `uk_pedido_comensal_activo`) vienen después: son los fáciles una
vez montada la fixture.

### 4.4 La comparación es estructurada, no un diff de texto

**Esto se aprendió corriéndolo, no razonándolo** (§5.5). Un `diff` sobre la salida cruda de
`pg_dump` es casi todo ruido: el snapshot fue volcado por otra versión de `pg_dump`, así que
restricciones **idénticas** se escriben distinto —

```
ARRAY['ABIERTA'::character varying, ...]::text[]     ← una versión
ARRAY[('ABIERTA'::character varying)::text, ...]     ← la otra
```

— y en la corrida del 2026-09-09 salieron ~10 diferencias cosméticas contra 3 reales. Un test
escrito así nace en rojo permanente y se apaga a la semana.

La comparación va contra el catálogo, normalizada: `pg_get_constraintdef` para restricciones,
`pg_indexes` para índices, `information_schema.columns` para columnas, `pg_proc` para funciones
y `pg_trigger` para triggers. Se comparan **conjuntos ordenados de tuplas**, no líneas de texto.

### 4.5 Proyecto aparte, y se saltea sin Docker

**`Atipico.Database.Tests`, no dentro de `Atipico.Infraestructure.Tests`**, que hoy es Moq puro
y rápido (`Moq.EntityFrameworkCore` para los `DbSet`). Mezclar una suite lenta que necesita
Docker con una que corre en milisegundos degrada las dos.

**Sin Docker la suite se saltea, no falla.** Es una condición, no una preferencia: el 2026-09-09
el demonio de Docker estaba caído al empezar la conversación, y
`specs/script-inicial-completo.md` §5 quedó sin verificar en su momento por lo mismo. Si
`dotnet test` en la raíz se pone rojo en una máquina sin Docker, la suite se ignora en una
semana y el proyecto queda peor que antes de tenerla.

### 4.6 Lo que el contenedor **no** prueba

El contenedor es PostgreSQL vanilla; **Neon no lo es** (pooler, disponibilidad de extensiones,
`channel_binding` obligatorio en la cadena de conexión). Estos tests prueban que *el esquema es
coherente consigo mismo y con el C#*. **No** prueban que Neon se comporte idéntico. Eso lo
sigue cubriendo el usuario, probando contra Neon después de aplicar.

## 5. Bitácora de verificación — 2026-09-09

Todo lo de esta sección se ejecutó de verdad; no es diseño proyectado.

### 5.1 Docker y el contenedor

Docker Desktop estaba **caído** al comenzar (`failed to connect to the docker API at
npipe:////./pipe/dockerDesktopLinuxEngine`). El usuario lo levantó y se verificó:

- Server/Client `29.7.2`, `OSType linux`, 0 contenedores.
- `postgres:18-alpine` descargada; contenedor `atipico-db-probe` listo (`pg_isready`) en **~4s**.
- `PostgreSQL 18.6 on x86_64-pc-linux-musl` — la versión mayor coincide con la de producción.

El contenedor de sondeo se eliminó al terminar.

### 5.2 La cadena completa corre limpia — primera vez que se prueba

`script_inicial.sql` + `002`…`014`, uno por uno, con `-v ON_ERROR_STOP=1`: **los 14 en verde**,
sin un solo error. `specs/script-inicial-completo.md` §5 había dejado esto explícitamente
pendiente por no haber Docker disponible; queda saldado para la cadena (falta todavía la
verificación de `dev_datos_iniciales.sql`, que es de datos, no de esquema).

`schema_completo.sql` también cargó limpio en una segunda base del mismo contenedor: 16 objetos
en `information_schema.tables` (11 tablas + 5 vistas).

### 5.3 Cadena vs snapshot: tres diferencias reales, en direcciones opuestas

`pg_dump --schema-only --no-owner --no-privileges` de las dos bases (1220 vs 1270 líneas) y
comparación:

| Objeto | Cadena (`sql/*.sql`) | Desplegado (`schema_completo`) | Quién tiene razón |
|---|---|---|---|
| `fn_pedido_mesa_ocupada` / `tg_pedido_mesa_ocupada` | dropeados por `013` | **presentes** | la cadena → regenerar |
| `ck_usuario_rol` | incluye `DELIVERY` (`014`) | **no lo incluye** | la cadena → regenerar |
| `ck_cuenta_metodo` | `EFECTIVO, TARJETA, TRANSFERENCIA, YAPE, PLIN` | `EFECTIVO, TARJETA, TRANSFERENCIA, QR` | **el desplegado** → falta migración |

Los dos primeros son el snapshot atrasado: se arreglan regenerándolo (tarea 2 del §3). El
tercero va **al revés**, y es más grave.

### 5.4 El drift de `ck_cuenta_metodo`, y su consecuencia real

`sql/script_inicial.sql` línea 200 permite `('EFECTIVO','TARJETA','TRANSFERENCIA','YAPE','PLIN')`.
La base desplegada permite `('EFECTIVO','TARJETA','TRANSFERENCIA','QR')`. **No hay ninguna
migración que registre ese cambio** — `grep` sobre `sql/` lo confirma: `QR` aparece solo dentro
de `006`/`007`, usándolo, nunca agregándolo al `CHECK`.

Ya estaba diagnosticado: el encabezado de `schema_completo.sql` (líneas 42-46) lo llama *"DRIFT
CONOCIDO"* y sentencia *"Este archivo manda"*. `MetodoPago.cs` también lo comenta. Conocido, sí;
**reparado, no**.

**La consecuencia concreta, que no estaba escrita en ningún lado:** un ambiente nuevo construido
por la vía canónica —`script_inicial.sql` + migraciones numeradas— queda con un `CHECK` que
**rechaza `QR`**. Ahí no se puede cobrar por QR, y con eso se cae toda la feature de comprobantes
(`specs/comprobantes-qr.md`), cuyos triggers en `006`/`007` filtran por `metodo_pago = 'QR'`
sobre filas que nunca van a existir. A Neon no le pasa nada; le pasa a cualquier ambiente que
nazca de la cadena — justamente el escenario de la rama de Neon para desarrollo que
`specs/script-inicial-completo.md` deja como próximo paso.

Se repara con una migración **`015`**, nunca editando `script_inicial.sql`, que ya está aplicado.
Queda fuera del alcance de este spec: es un cambio a la base, y va con su propia aprobación.

### 5.5 El ruido cosmético de `pg_dump`

Ver §4.4. Se descubrió al mirar el diff: ~10 diferencias puramente tipográficas contra 3 reales,
porque los dos volcados salieron de versiones distintas de `pg_dump`. Es la razón por la que el
test de §4.3.1 compara catálogo normalizado y no texto — y es exactamente el tipo de detalle que
no se anticipa razonando, solo corriéndolo.

### 5.6 Implementación de `Atipico.Database.Tests` — 2026-09-10

Todo lo de esta sección se ejecutó de verdad, igual que §5.1-§5.5. `pg_get_constraintdef`
**no alcanza solo** para normalizar el catálogo (§4.4 lo daba por sentado); hicieron falta
tres capas más de normalización, cada una encontrada corriendo la suite, no leyendo el
código:

1. **`\restrict`/`\unrestrict`.** `psql`/`pg_dump` modernos envuelven todo el volcado en
   estos dos meta-comandos de *cliente* (verificado en `sql/schema_completo.sql:53` y
   `:1317`) — no son SQL, Npgsql (que habla el protocolo de cable, no el lenguaje de
   `psql`) revienta con `syntax error` al toparlos. Se filtra cualquier línea que empiece
   con `\` antes de ejecutar.
2. **`IN (...)` vs `= ANY (ARRAY[...])` no es solo un problema de versión de `pg_dump`.**
   Es estructural: Postgres guarda el árbol de expresión tal como se escribió el DDL
   original, y `pg_get_constraintdef` lo reproduce sin colapsar las dos formas —
   `script_inicial.sql` escribe `IN (...)`, `schema_completo.sql` (salida literal de
   `pg_dump`) siempre usa `= ANY (ARRAY[...])`, con un cast por elemento
   (`('MESERO'::character varying)::text`) más un paréntesis extra que la forma de la
   cadena no tiene. Sin normalizar esto, las 8 restricciones de lista fallan por sintaxis
   y ahogan la única diferencia real (`ck_usuario_rol` sin `DELIVERY`) en puro ruido.
3. **CRLF dentro de `$function$...$function$`.** `sql/script_inicial.sql` está en un
   checkout de Windows; dentro de un cuerpo con dollar-quoting Postgres guarda el `\r`
   *literal*, no lo trata como fin de línea a normalizar. El mismo cuerpo, volcado por
   `pg_dump` en Linux, no lo tiene — mismo código, bytes distintos. Se detectó comparando
   `fn_cuenta_inmutable` con `diff`: visualmente idéntico, `file` marcaba un lado
   `"with CRLF, LF line terminators"` y el otro `"ASCII text"` a secas.
4. **Líneas en blanco entre sentencias, ya resuelto el CRLF.** `schema_completo.sql` trae
   una línea vacía entre cada sentencia de varios triggers (`fn_cuenta_inmutable`,
   `fn_detalle_inmutable`, `fn_pedido_plato_facturado`, `fn_touch`) que
   `script_inicial.sql`, tal como está hoy en el repo, ya no tiene — semánticamente inerte
   en PL/pgSQL, confirmado byte a byte reconstruyendo las dos bases a mano con `psql` y
   diffeando. Se resuelve colapsando todo run de espacio en blanco a uno solo, después de
   sacar los literales (paso 2) para no tocar el contenido de un mensaje de error.

Con las cuatro capas, `CadenaVsSnapshotTests` deja de fallar por ruido y falla por lo real:
**dos** diferencias, no las tres que predecía §5.3 —
`fn_pedido_mesa_ocupada`/`tg_pedido_mesa_ocupada` (dropeados por `013`, ausentes de la
cadena, presentes en el snapshot) y `ck_usuario_rol` sin `DELIVERY` (`014`)—. La tercera,
`ck_cuenta_metodo`, ya no aparece: `015` (`specs/reparacion-ck-cuenta-metodo.md`, en
producción el mismo día) la cerró, así que hoy cadena y snapshot coinciden ahí.

**Otros dos hallazgos, de armado, no del dominio:**

- **Orden de inicialización.** La primera versión de `PostgresFixture` armaba
  `SnapshotOwnerConnectionString` *después* de llamar al método que la usaba —
  `InvalidOperationException: The ConnectionString property has not been initialized`,
  en las 16 pruebas a la vez. Se corrigió armando las tres cadenas de conexión antes de
  cualquier `AplicarXAsync`.
- **`xUnit v2.9.x` no tiene `Assert.Skip`/`SkipUnless` dinámico** (eso es de v3). El
  mecanismo real es setear `Skip` en el constructor de un `FactAttribute`/`TheoryAttribute`
  propio — se ejecuta en tiempo de *descubrimiento*, antes de correr nada — de ahí
  `DockerFactAttribute`/`DockerTheoryAttribute`.

**Verificado, no solo compilado:** 16 tests con Docker arriba (13 verde, 3 rojo por la razón
correcta); 9 de 9 `Skipped` con Docker inalcanzable a propósito (PATH vaciado para el
proceso de test, sin tocar el demonio real — el contenedor persistente de
`docker-compose.db.yml` del usuario no se interrumpió); `dotnet test` de toda la solución,
199 pruebas previas sin cambios + esto.

**Regenerar `sql/schema_completo.sql` no es tarea bloqueada del agente — es tarea del
usuario.** Necesita `pg_dump --schema-only` contra Neon (§2.3), y la regla final del
2026-09-10 (§2.2) es que el agente no se conecta a Neon bajo ningún concepto salvo
pedido explícito del usuario en el momento — no algo que espera a que consiga una
credencial. El camino quedó claro después de que la credencial local se desactualizara
tras una rotación (`specs/postgres-local-dev.md` §8.1): en vez de perseguir una nueva,
el usuario decidió que directamente no hace falta que el agente la tenga.

## 6. Diseños descartados

### 6.1 "Mantener a mano un script principal"

Era el pedido original. Se descartó porque el script principal ya existe
(`sql/schema_completo.sql`) y su contrato es **generarse**, no mantenerse (§2.4). El trabajo
correcto no es *actualizar*, es *regenerar y verificar*.

### 6.2 Un servicio `postgres` en `docker-compose.yml`

Descartado por estado persistente y puerto fijo (§4.1).

### 6.3 Los tests dentro de `Atipico.Infraestructure.Tests`

Descartado: contaminaría con Docker y con segundos una suite que hoy es Moq y milisegundos
(§4.5).

### 6.4 Tests de "corrupción de datos"

Descartado del alcance, y no por tamaño sino por categoría. Dos motivos:

1. **El contenedor nace vacío**: no hay nada corrupto que encontrar en él.
2. **Un test que asserta contra datos vivos no es un test, es un monitor disfrazado**: falla por
   razones que no tienen que ver con el código, en máquinas que pueden no tener credenciales, y
   rompe CI.

La consistencia de datos vivos ya tiene su mecanismo, y funciona: `v_cuenta_descuadrada` y
`v_pedido_plato_sin_cobrar`, vistas de solo lectura servidas por `ReportesController` y
mostradas en `Reportes/CuentasDescuadradas.razor`. **El agente `db` puede ser dueño de vistas
nuevas de ese tipo** — son reportes, no tests. Dos trabajos, dos mecanismos; mezclarlos es lo
que rompe CI.

### 6.5 Unificar de verdad la fuente de verdad enum ↔ `CHECK`

Tentador —generar el `CHECK` desde el enum, o al revés— y descartado para la v1: obliga a un
paso de generación en el build o a un `dotnet` corriendo durante la migración, y el problema real
no es escribir los dos lugares, es **enterarse cuando divergen**. El test de §4.3.2 resuelve eso
con una fracción del costo. Si algún día hay muchos más enums, se reconsidera.

## 7. La memoria del agente

Con `memory: project` en el *frontmatter*, el agente recibe `.claude/agent-memory/db/`
automáticamente — la carpeta aparte que pedía el ticket ya viene por diseño, no hay que
construirla.

**Restricción de seguridad:** esa carpeta va a git y se comparte con el equipo. **Ninguna cadena
de conexión, contraseña ni hash** puede vivir ahí. Las credenciales siguen donde ya están: user
secrets en desarrollo, variables de entorno en cualquier despliegue real.

Qué sí va en su memoria: dónde vive cada cosa en `sql/`, qué drift ya se diagnosticó y con qué
resultado, qué decidió el usuario cuando cadena y snapshot se contradijeron, y las trampas que
solo se descubren corriendo (el ruido de `pg_dump`, el tiempo de arranque del contenedor).

## 8. Criterios de aceptación

- [x] `.claude/agents/db.md` existe, con la frontera de §2.1, la regla dura de §2.2 y el
      entregable de tres partes de §2.3 escritos explícitamente. **Hecho el 2026-09-09.**
- [x] El agente arranca en frío sabiendo las trampas: Neon, `app_restaurante` sin `DELETE`,
      migraciones aplicadas inmutables, `UPPER_SNAKE_CASE` del converter, `psql` fuera del PATH
      en `C:\Program Files\PostgreSQL\18\bin\`, `timestamptz` solo UTC. **Hecho** — sección
      "Trampas del entorno", más la receta del contenedor y el estado de deriva conocido, para
      que no tenga que redescubrirlo en cada arranque en frío.
- [x] `Atipico.Database.Tests` existe, referenciado en `Atipico.slnx`. **Hecho el
      2026-09-10.**
- [x] Con Docker levantado, la suite corre y **el test de §4.3.1 falla**, señalando
      diferencias reales de §5.3 — falla por la razón correcta, no por un error de armado.
      **Hecho** — con un matiz que corrige lo anticipado: hoy son **dos** diferencias, no
      tres (`fn_pedido_mesa_ocupada`/`tg_pedido_mesa_ocupada` por `013`, `ck_usuario_rol`
      sin `DELIVERY` por `014`), no las tres originales — `ck_cuenta_metodo` **ya no
      difiere**, porque `015` (`specs/reparacion-ck-cuenta-metodo.md`, en producción)
      cerró ese drift el mismo día. Ver §5.6.
- [x] Sin Docker, `dotnet test` en la raíz **no se pone rojo**: la suite se saltea y lo
      informa. **Hecho y verificado** — 9 de 9 tests `Skipped`, cero `Failed`, con Docker
      inalcanzable a propósito (no se apagó el demonio real, ver §5.6).
- [x] El test de §4.3.2 pasa hoy (la relación es subconjunto, y los ocho enums con `CHECK`
      —no solo `MetodoPago`— cumplen contra el catálogo real del contenedor, no contra
      texto de `sql/`). **Hecho.**
- [x] El test de `app_restaurante` confirma que el `DELETE` falla con
      `insufficient_privilege` (`42501`), y que `INSERT`/`UPDATE` sí funcionan (el
      contraste importa: el rol no está roto, le falta *justo* `DELETE`). **Hecho.**
- [ ] `sql/schema_completo.sql` regenerado y al día con `014`; el test de §4.3.1 pasaría a
      verde por completo (`ck_cuenta_metodo` ya no es la excepción que este criterio
      anticipaba — ver arriba). **Tarea del usuario, no del agente**: pide `pg_dump
      --schema-only` contra Neon, y el agente no se conecta ahí (§2.2) — ver §5.6.

## 9. Fuera de alcance — lo que viene después

- **Migración `015`** para `ck_cuenta_metodo` (§5.4). Necesita spec propio y aprobación.
- **Verificar `dev_datos_iniciales.sql`** de punta a punta en el contenedor: lo que
  `specs/script-inicial-completo.md` §6 dejó en checklist sin marcar. Ahora hay con qué.
- **La rama de Neon para desarrollo** (`specs/cicd-github-azure-render.md` §3.2). Si nace de la
  cadena en vez del snapshot, hereda el bug de QR de §5.4 — otra razón para reparar el drift
  antes.
