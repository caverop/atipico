# Limpieza de datos de prueba en la base compartida

Especificación para vaciar los datos de prueba acumulados en la base de Neon que hoy
sirve **QA y producción a la vez** (ver `specs/cicd-github-azure-render.md` §3.1–3.2),
antes de separar los entornos con una rama de Neon dedicada a desarrollo.

- **Estado:** propuesto, pendiente de aprobación para ejecutar. Auditoría hecha (§2);
  §5 ya decidido (renombrar los 4 usuarios); falta correr los dos scripts.
- **Alcance:** vaciar `turno_caja`, `pedido`, `pedido_mesa`, `pedido_plato`, `cuenta`,
  `detalle_cuenta`, `comprobante_pago`; devolver las mesas a `LIBRE`; renombrar los 4
  `usuario` de prueba a cuentas de operación (§5).
- **Fuera de alcance:** crear la rama de Neon para desarrollo (siguiente tarea, no
  esta); limpiar los archivos huérfanos en R2 (§6, queda como paso manual aparte);
  tocar `plato`, `tipo_plato` o las filas de `mesa` (se quedan igual, sin marca de
  prueba en el nombre).

Jira: [SCRUM-5 — Clean Database](https://caverop.atlassian.net/browse/SCRUM-5).

---

## 1. Problema

`SCRUM-5` pide "limpiar de datos de prueba la base de datos", sin más detalle — el
ticket no tiene descripción. Esta base **no es un entorno de desarrollo aislado**:
`specs/cicd-github-azure-render.md` §3.1 documenta que QA (Render, rama `develop`) y
producción (Azure, rama `master`) leen y escriben la misma base de Neon, por decisión
explícita, hasta que exista una rama de Neon dedicada.

Eso cambia el problema. Ya existe `sql/dev_limpieza_transaccional.sql`, escrito para
"nunca ejecutarse contra datos reales" — pensado para una base de desarrollo sin
usuarios reales detrás. Correrlo tal cual, sin verificar antes qué hay en las tablas,
sería asumir que nada de lo que hay es real. Con QA y producción compartiendo base,
esa suposición no se puede dar por sentada: hay que **demostrarla**, no asumirla.

## 2. Auditoría — lo que hay hoy, verificado

Se inspeccionó cada tabla transaccional antes de proponer nada. Resultado: **el 100%
de los datos transaccionales es de prueba**, con evidencia mecánica, no apariencia:

| Tabla | Filas | Por qué es de prueba |
|---|---|---|
| `turno_caja` | 1 | único turno, abierto por `test.cajero` |
| `pedido` | 16 | los 16 los tomó **el mismo mesero, `test.mesero`**; el comensal es `pamela`, `pamela2`…`pamela4`, `pemal8`/`pemal9` (typos), `Test9`, `Test87`, `test46`, `testfile`, `testfile2` — nombres de prueba secuenciales, no personas |
| `cuenta` | 13 | **las 13 cuentas facturan exactamente Bs 22,00** — el precio de "Causa limeña", el primer plato del catálogo. Trece comensales distintos pagando la misma cifra exacta no ocurre por azar; es el resultado de probar el flujo con el primer plato de la lista cada vez |
| `detalle_cuenta` | 13 | una línea por cuenta, coherente con lo anterior |
| `comprobante_pago` | 7 | comprobantes QR subidos durante las pruebas de esa feature |

No se encontró ninguna fila que rompa el patrón: ningún otro mesero, ningún otro
cajero, ningún monto distinto. La conclusión no es una suposición razonable — es lo
que las filas dicen.

**Esta auditoría tiene fecha.** Verificada el día de este spec. Si pasa tiempo entre
esto y la ejecución, hay que repetirla — es precisamente el paso que reemplaza a
"total, es dev" por evidencia concreta, y evidencia vieja no sirve para una base que
sigue recibiendo tráfico de QA mientras tanto.

## 3. Decisión

Vaciar las 7 tablas transaccionales con el mismo mecanismo que ya usa
`sql/dev_limpieza_transaccional.sql` — `TRUNCATE ... RESTART IDENTITY CASCADE`, no
`DELETE` — y devolver a `LIBRE` las mesas que la prueba dejó `OCUPADA`. Se hace en un
script nuevo, `sql/dev_limpieza_qa_confirmada.sql`, en vez de reutilizar el existente
sin cambios: correrlo contra una base que sirve tráfico real necesita una compuerta de
confirmación que el script original no tiene y no necesitaba, porque nunca antes
corrió contra algo que no fuera un descarte de desarrollo.

### 3.1 Por qué `TRUNCATE` y no `DELETE`, otra vez

`fn_detalle_inmutable` y `fn_cuenta_inmutable` rechazan cualquier `DELETE` con filas en
`detalle_cuenta` y `cuenta`; todas las FK son `ON DELETE RESTRICT`. `TRUNCATE` no
dispara triggers de fila, que es exactamente para lo que sirve acá — igual que en el
script original.

### 3.2 Por qué una compuerta de confirmación, y no confiar en "ya lo pensé"

`dev_abrir_turno.sql` ya usa el patrón `\set`/`\if`/`RAISE EXCEPTION` para plantarse
sin tocar nada si algo no está en orden; acá corresponde el mismo idioma, subido un
escalón: el script exige un parámetro con una frase exacta, no solo un flag en cero o
uno, para que ejecutarlo por descuido (`psql -f sql/dev_limpieza_qa_confirmada.sql`
sin el parámetro) sea imposible, y para que copiarlo y pegarlo sin leer el `-v`
tampoco alcance.

```
-v confirmo=SI_VERIFIQUE_QUE_ES_TODO_PRUEBA
```

Sin ese parámetro, o con cualquier otro valor, el script aborta con `RAISE EXCEPTION`
antes del `BEGIN` — código de salida 3, nada tocado.

### 3.3 Las mesas necesitan un `UPDATE` explícito, y no es solo por hoy

`mesa` **no tiene ningún trigger** (verificado: `pg_trigger` para esa tabla devuelve
cero filas) y nada la vincula a `pedido_mesa` por cascada. Verificado también en la
auditoría: de las 4 mesas `OCUPADA`, **solo la mesa 1 tiene una fila en `pedido_mesa`**
— las mesas 2, 3 y 4 están `OCUPADA` sin ningún pedido que las respalde. El estado ya
está desincronizado *hoy*, con datos de prueba todavía en la base: vaciar
`pedido_mesa` no las va a corregir, porque no dependían de ella.

Esto no es un problema nuevo que esta limpieza introduce — es uno que ya existe y que
la limpieza expone. `CLAUDE.md` ya lo documenta como propiedad del sistema: `mesa.estado`
se libera a mano en `PedidosController`, nunca por trigger. El script agrega:

```sql
UPDATE mesa SET estado = 'LIBRE' WHERE estado <> 'LIBRE';
```

## 4. `sql/dev_limpieza_qa_confirmada.sql`

```sql
-- =====================================================================
-- dev_limpieza_qa_confirmada.sql — vacía los datos de prueba de la base
-- compartida entre QA y producción (specs/cicd-github-azure-render.md §3.1).
--
-- SIN NUMERAR A PROPÓSITO, igual que dev_limpieza_transaccional.sql: no es
-- historia de migración, es una herramienta de una sola vez.
--
-- A DIFERENCIA de dev_limpieza_transaccional.sql, esto NO corre contra un
-- descarte de desarrollo: corre contra la base que QA y producción comparten
-- hoy (specs/cicd-github-azure-render.md §3.2). La auditoría que justifica
-- que las filas de hoy son 100% de prueba está en
-- specs/limpieza-datos-prueba.md §2 — léela antes de correr esto de nuevo
-- con datos distintos a los que ahí se verificaron.
--
-- Requiere SUPERUSUARIO, igual que el script original: app_restaurante no
-- tiene TRUNCATE ni DELETE sobre ninguna de estas tablas.
-- =====================================================================

\set ON_ERROR_STOP on

-- Compuerta: exige la frase exacta, no un simple 0/1. Mismo idioma que
-- dev_abrir_turno.sql (\set / \if / RAISE EXCEPTION), un escalón más estricto
-- porque acá el costo de un descuido es mayor: esta base sirve QA y
-- producción, no un sandbox de desarrollo.
\if :{?confirmo}
\else
    \set confirmo ''
\endif

-- \if no admite comparaciones de igualdad directamente (probado: con
-- ":{'var'} = 'literal'" psql tira "se esperaba booleano" SIEMPRE, coincida o
-- no el valor -- una version anterior de este script tenia exactamente ese
-- error). El patron correcto, ya usado en dev_abrir_turno.sql, es resolver la
-- comparacion con un SELECT y recien testear el booleano resultante.
SELECT :'confirmo' = 'SI_VERIFIQUE_QUE_ES_TODO_PRUEBA' AS confirmado
\gset

\if :confirmado
\else
    \warn 'Este script vacía datos en la base COMPARTIDA de QA y producción.'
    \warn 'Antes de correrlo, repetí la auditoría de specs/limpieza-datos-prueba.md #2'
    \warn 'contra el estado ACTUAL de la base -- no contra esta fecha.'
    \warn 'Si sigue siendo 100% prueba, corre con:'
    \warn '  -v confirmo=SI_VERIFIQUE_QUE_ES_TODO_PRUEBA'
    DO $$ BEGIN RAISE EXCEPTION 'Confirmacion faltante o incorrecta. Nada fue modificado.'; END $$;
\endif


-- ---------------------------------------------------------------------
-- 1. Vaciar lo transaccional
-- ---------------------------------------------------------------------
-- Mismo mecanismo y misma lista que dev_limpieza_transaccional.sql: TRUNCATE
-- no dispara fn_detalle_inmutable ni fn_cuenta_inmutable, que rechazarian
-- cualquier DELETE. RESTART IDENTITY: los id vuelven a 1.
BEGIN;

TRUNCATE
    comprobante_pago,
    detalle_cuenta,
    cuenta,
    pedido_plato,
    pedido_mesa,
    pedido,
    turno_caja
    RESTART IDENTITY CASCADE;

-- mesa no tiene trigger propio y no depende de pedido_mesa por cascada
-- (ver especificacion, seccion 3.3): sin este UPDATE, una mesa que quedo
-- OCUPADA por una prueba sigue OCUPADA para siempre, sin ningun pedido que
-- la explique.
UPDATE mesa SET estado = 'LIBRE' WHERE estado <> 'LIBRE';

COMMIT;

-- Los catalogos NO se tocan: usuario, plato, tipo_plato y las filas de mesa
-- quedan intactos. Ver specs/limpieza-datos-prueba.md, seccion 5 -- es una
-- decision que se dejo explicita y abierta, no un descuido.
--
-- comprobante_pago.storage_key apunta a archivos en el bucket de R2
-- compartido (atipico-comprobantes, specs/cicd-github-azure-render.md 3.3):
-- este TRUNCATE no los borra. Capturalos ANTES de correr esto -- ver
-- specs/limpieza-datos-prueba.md, seccion 6.

\echo ''
\echo 'Listo. Verifica con:'
\echo '  SELECT (SELECT count(*) FROM pedido) AS pedidos, (SELECT count(*) FROM cuenta) AS cuentas,'
\echo '         (SELECT count(*) FROM mesa WHERE estado <> ''LIBRE'') AS mesas_no_libres;'
\echo 'Las tres columnas deben dar 0.'
```

## 5. Catálogos — decisión tomada para `usuario`

`usuario` tenía 4 filas: `Test Admin`, `Test Mesero`, `Test Cajero`, `Test Cocinero`
(`test.admin`, `test.mesero`, `test.cajero`, `test.cocinero`), creadas el 2026-08-19.
El nombre decía "Test" tan explícito como los comensales de §2 — por el nombre
solo, calificarían igual como "datos de prueba". La diferencia es que estas filas
**no eran ruido, eran la única puerta de entrada a la aplicación**: borrarlas
dejaba QA sin ninguna cuenta con la que iniciar sesión, el mismo motivo por el que
`dev_limpieza_transaccional.sql` ya las excluía.

**Se eligió el camino (b): renombrarlas a cuentas de operación**, en el mismo rol
que ya tenían — un reemplazo 1:1, no una reestructuración:

| Antes | Después | Rol |
|---|---|---|
| `test.admin` | `admin` | `ADMIN` |
| `test.mesero` | `mesera` | `MESERO` |
| `test.cajero` | `delivery` | `CAJERO` |
| `test.cocinero` | `cocinera` | `COCINERO` |

`delivery` fue la decisión que necesitó confirmarse: el sistema solo conoce cuatro
roles (`Mesero`, `Cajero`, `Cocinero`, `Admin` — `Atipico.Domain/Enums/RolUsuario.cs`)
y `Delivery` no es uno de ellos, es un **tipo de pedido** (`ck_pedido_tipo` incluye
`DELIVERY`), no un rol de usuario. Se confirmó que `delivery` ocupa el lugar de
`test.cajero`: cobra cuentas y abre/cierra turno de caja, igual que antes.

Contraseña para los cuatro: **`atipico`**, compartida. Se documenta así, sin
suavizarlo: es una contraseña trivial y repetida en una cuenta `ADMIN` de una base
que sirve producción. Se acepta como credencial de arranque, no como política final
— quien la reciba debería poder cambiarla la primera vez que entra. Hoy la pantalla
de `Usuarios/Edit.razor` no fuerza ningún cambio de contraseña en el primer login;
si esto sigue siendo así por mucho tiempo, vale la pena revisarlo aparte —
deliberadamente fuera de alcance de este spec.

`plato`, `tipo_plato` y las 4 filas de `mesa` no llevan ninguna marca de prueba en el
nombre (son un menú peruano y una numeración de mesas con forma real): quedan
intactas, sin tocar.

### 5.1 `sql/dev_renombrar_usuarios.sql`

A diferencia del script de §4, este **no necesita superusuario**:
`app_restaurante` ya tiene `UPDATE` sobre `usuario` (`SELECT, INSERT, UPDATE`,
verificado). Es un `UPDATE` en el lugar — mismo `id`, mismo rol, cambia
`nombre`, `nombre_usuario` y `password_hash` — así que ninguna fila que ya
referencia a estos usuarios por `id` (`pedido.id_mesero`, `turno_caja.id_cajero`,
etc.) se ve afectada por el cambio de nombre. Puede correr antes o después del
script de §4, en cualquier orden: son tablas distintas.

Los cuatro hashes son **BCrypt real**, generados con `BCrypt.Net-Next 4.1.0` — la
misma versión exacta que usa `BCryptPasswordHasher.Hash()` en
`Atipico.Infraestructure` — y cada uno se verificó con `BCrypt.Verify("atipico", …)`
en el mismo proceso que lo generó, antes de escribirlo acá. No son hashes de
ejemplo: son los que va a comparar el login la primera vez que alguien entre como
`admin`/`atipico`.

`actualizado_en` no se toca a mano: `usuario` tiene `tg_touch_usuario`
(`fn_touch`), que la actualiza sola en cualquier `UPDATE`.

```sql
-- =====================================================================
-- dev_renombrar_usuarios.sql — reemplaza los 4 usuarios de prueba por
-- cuentas de operación, mismo rol, mismo id.
-- Ver specs/limpieza-datos-prueba.md §5.
--
-- SIN NUMERAR A PROPÓSITO: no cambia esquema, es una carga de datos de una
-- sola vez. NO requiere superusuario — app_restaurante tiene UPDATE sobre
-- usuario.
-- =====================================================================

\set ON_ERROR_STOP on

BEGIN;

UPDATE usuario SET
    nombre = 'Admin',
    nombre_usuario = 'admin',
    password_hash = '$2a$11$iuLT5ZWur.miWwB1iMAflua3310KofI3edAF8mSO.2rIdUxVLQwPq'
WHERE nombre_usuario = 'test.admin';

UPDATE usuario SET
    nombre = 'Mesera',
    nombre_usuario = 'mesera',
    password_hash = '$2a$11$1sbwdoeMh2Ai.DjJUJLwcOwvSuYRN9RWg15F167AAOGhxcG1wqL8q'
WHERE nombre_usuario = 'test.mesero';

-- Ocupa el lugar de test.cajero, mismo rol CAJERO. "Delivery" es un tipo de
-- pedido (ck_pedido_tipo), no un rol de usuario: el sistema solo tiene
-- Mesero/Cajero/Cocinero/Admin. Confirmado con el usuario antes de escribir
-- esto — ver specs/limpieza-datos-prueba.md §5.
UPDATE usuario SET
    nombre = 'Delivery',
    nombre_usuario = 'delivery',
    password_hash = '$2a$11$h1S719qaOUpP4F3vocaicuBwJBhoehMCCTsxXLTfr1HZqUDGvxAnC'
WHERE nombre_usuario = 'test.cajero';

UPDATE usuario SET
    nombre = 'Cocinera',
    nombre_usuario = 'cocinera',
    password_hash = '$2a$11$hr1zcVE8Tvrvsj.vSO0fe.HSM8gASRQlr0otqQHvLQ/pPie8h1bHW'
WHERE nombre_usuario = 'test.cocinero';

COMMIT;

\echo ''
\echo 'Listo. Verificá con:'
\echo '  SELECT id, nombre, nombre_usuario, rol FROM usuario ORDER BY id;'
\echo 'Las cuatro filas deben mostrar admin/mesera/delivery/cocinera, con el rol de siempre.'
```

## 6. Los archivos en R2 — capturar antes de vaciar

Los 7 `comprobante_pago` de hoy apuntan a objetos reales en el bucket compartido
`atipico-comprobantes` (specs/cicd-github-azure-render.md §3.3 — el mismo bucket
que usa producción, no uno de desarrollo aparte). El `TRUNCATE` de §4 borra las
filas, no los archivos: a diferencia de cuando este mismo caso apareció en
`dev_limpieza_transaccional.sql` (huérfanos "inofensivos" en un bucket de
desarrollo), acá son archivos reales en el bucket que sirve producción.

Antes de correr el script de §4, guardar esta lista para poder borrarlos del bucket
después, a mano o con el cliente de R2 (fuera de alcance de este spec, que es sobre la
base):

```sql
SELECT storage_key FROM comprobante_pago ORDER BY id;
```

```
comprobantes/4/01a052f2525572a3ba9fe3852a887a81.webp
comprobantes/5/01a05486e94a7070a69e60835b8067e0.webp
comprobantes/6/01a054a2d77176d9a61aca099c20b7ff.webp
comprobantes/7/01a054acf15c7464b7dcdb2634c9652c.webp
comprobantes/11/01a068ead848773fbdf4903ecabc691d.webp
comprobantes/12/01a06d3e21f079d58eaf0df5ccc1613f.webp
comprobantes/13/01a06d42d94270d18744306601681af5.webp
```

## 7. Red de seguridad — no hay backup hoy

No existe todavía ninguna rama de Neon ni copia de la base (es justamente lo que la
tarea siguiente del usuario va a crear). Si la auditoría de §2 estuviera equivocada,
no hay forma de deshacer el `TRUNCATE` más que el *point-in-time recovery* que Neon
retiene por defecto (ventana corta, verificar el plan del proyecto antes de asumirla).

**Recomendado, no obligatorio:** antes de correr §4, crear una rama de Neon de
descarte (`neonctl branches create --name pre-limpieza-2026-09`) como respaldo de un
minuto. No es la rama de desarrollo que el usuario va a crear después — es más barata,
se puede borrar apenas se confirme que la limpieza salió bien, y no cambia en nada el
orden que el usuario ya definió (primero limpiar, después separar los entornos).

## 8. Verificación

- [ ] La auditoría de §2 se repitió contra el estado actual de la base, no se asumió de este documento.
- [x] §5 decidido: renombrar `usuario` (admin/mesera/delivery/cocinera), rol preservado.
- [ ] `sql/dev_renombrar_usuarios.sql` corrido y verificado (§5.1).
- [ ] Las claves de R2 de §6 quedaron guardadas antes del `TRUNCATE`.
- [ ] `sql/dev_limpieza_qa_confirmada.sql` corrido con el parámetro `-v confirmo=...` exacto.
- [ ] Post-verificación: `pedido`, `cuenta`, `detalle_cuenta`, `comprobante_pago`, `pedido_plato`, `pedido_mesa`, `turno_caja` en 0 filas.
- [ ] Las 4 mesas en `LIBRE`.
- [ ] La aplicación abre sin turno de caja activo — es el estado esperado (`specs/numero-pedido.md` §8.4), no un error: hay que abrir un turno antes de cargar el primer pedido nuevo.

## 9. Próximo paso — fuera de alcance de este spec

El usuario ya definió el orden: primero esta limpieza, después crear una rama de Neon
dedicada a desarrollo, separada de QA/producción. Esa separación es justamente lo que
`specs/cicd-github-azure-render.md` §3.2 y `specs/deploy-azure-aspire.md` §5.3 ya
dejan anotado como pendiente ("la rama de Neon queda para un ciclo próximo"). Ese
trabajo tiene su propio spec — no se diseña acá.
