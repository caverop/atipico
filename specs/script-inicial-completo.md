# Script de arranque completo — esquema + catálogo + usuarios de operación

Especificación para un script que deja un ambiente nuevo **listo para usar**, no solo con el
esquema creado. Pensado para inicializar la próxima rama de Neon dedicada a desarrollo
(`specs/cicd-github-azure-render.md` §3.2, `specs/deploy-azure-aspire.md` §5.3), que hoy sigue
pendiente.

- **Estado:** propuesto, pendiente de aprobación para ejecutar contra un ambiente real.
  Verificado por lectura contra el esquema en vivo; **no verificado de punta a punta en un
  contenedor** — Docker no estaba disponible al escribir esto (§5).
- **Alcance:** un script nuevo, `sql/dev_datos_iniciales.sql`, que corre **después** de
  `sql/schema_completo.sql` y carga: el catálogo (`tipo_plato`, `plato`, `mesa`) y los 4
  usuarios de operación ya decididos en `SCRUM-9`/`specs/limpieza-datos-prueba.md` §5
  (`admin`/`mesera`/`delivery`/`cocinera`, contraseña `atipico`).
- **Fuera de alcance:** regenerar `sql/schema_completo.sql` (ya se hizo, 2026-08-31, está al
  día hasta `012`); crear la rama de Neon en sí (siguiente tarea, no esta).

Jira: [SCRUM-12 — Create a initial script to database](https://caverop.atlassian.net/browse/SCRUM-12).

---

## 1. Problema

`sql/schema_completo.sql` ya resuelve la mitad del problema: es un snapshot de
`pg_dump --schema-only`, regenerado el 2026-08-31 y al día hasta la migración `012` (11
tablas, 15 índices, 8 funciones, 10 triggers, 5 vistas, más el `BLOQUE 2` que crea el rol
`app_restaurante` con sus permisos). Correrlo deja una base **con esquema y sin una sola
fila**.

Eso no alcanza para un ambiente nuevo que se quiere usar de una vez: sin `tipo_plato` ni
`plato` no hay menú que mostrar; sin `mesa` no hay dónde sentar a nadie; y sin `usuario` la
aplicación es inentrable — el primer `POST /auth/login` no tiene con qué autenticar a nadie.
Faltaría cargar todo eso a mano, por la interfaz, antes de poder abrir el primer turno.

Esto importa ahora en concreto: `SCRUM-5` va a dejar la rama de Neon para desarrollo como el
próximo paso, una vez terminada la limpieza. Esa rama nueva necesita nacer con algo más que
tablas vacías.

## 2. Decisión

Un script nuevo, **separado de `schema_completo.sql`**, que se corre a continuación:

```
psql <conexión> -v ON_ERROR_STOP=1 -f sql/schema_completo.sql
psql <conexión> -v ON_ERROR_STOP=1 -f sql/dev_datos_iniciales.sql
```

### 2.1 Por qué separado, y no agregado dentro de `schema_completo.sql`

`schema_completo.sql` es **salida literal de `pg_dump`**, y su propio encabezado lo dice:
se regenera completo cada vez que hace falta ponerlo al día con una migración nueva. Si los
`INSERT` de catálogo vivieran ahí adentro, la próxima regeneración por `pg_dump` los borraría
sin que nadie lo note — `pg_dump --schema-only` no vuelve a escribirlos porque nunca los leyó
de una base real, los escribió a mano quien regeneró el archivo. Separado, un archivo se
regenera solo y el otro se edita a mano sin que se pisen.

Es el mismo criterio que ya separa `dev_limpieza_transaccional.sql` de
`dev_renombrar_usuarios.sql`: una tabla, una responsabilidad, un archivo.

### 2.2 Qué carga, y de dónde salió

**No es catálogo inventado.** Es el que hoy está operando en la base compartida de QA/
producción (auditado en `specs/limpieza-datos-prueba.md` §2), la misma carta con la que el
restaurante ya está probando el sistema:

```sql
INSERT INTO tipo_plato (nombre) VALUES
    ('Entradas'), ('Fondos'), ('Postres'), ('Bebidas');

INSERT INTO plato (nombre, precio, id_tipo_plato, estado) VALUES
    ('Causa limeña',         22.00, (SELECT id FROM tipo_plato WHERE nombre = 'Entradas'), 'DISPONIBLE'),
    ('Papa a la huancaína',  18.00, (SELECT id FROM tipo_plato WHERE nombre = 'Entradas'), 'DISPONIBLE'),
    ('Lomo saltado',         45.00, (SELECT id FROM tipo_plato WHERE nombre = 'Fondos'),   'DISPONIBLE'),
    ('Ají de gallina',       38.00, (SELECT id FROM tipo_plato WHERE nombre = 'Fondos'),   'DISPONIBLE'),
    ('Suspiro a la limeña',  16.00, (SELECT id FROM tipo_plato WHERE nombre = 'Postres'),  'DISPONIBLE'),
    ('Chicha morada jarra',  15.00, (SELECT id FROM tipo_plato WHERE nombre = 'Bebidas'),  'DISPONIBLE');

INSERT INTO mesa (numero, capacidad, estado) VALUES
    (1, 4, 'LIBRE'), (2, 2, 'LIBRE'), (3, 6, 'LIBRE'), (4, 4, 'LIBRE');
```

**Los `id` no se fuerzan.** `tipo_plato`, `plato` y `mesa` son `GENERATED ALWAYS AS IDENTITY`
(`sql/script_inicial.sql`): en una base recién creada, sin filas previas, van a nacer
`1, 2, 3…` en el orden de inserción. Van a ser **distintos** de los `id` que hoy tienen las
mismas filas en la base compartida (`tipo_plato` arranca en `2` ahí, por historia acumulada) —
no importa: este script es para un ambiente que nace de cero, no un clon byte a byte.

**Mesa nace `LIBRE`, no `OCUPADA`.** En la base compartida las mesas quedaron `OCUPADA` por
pedidos de prueba (`specs/limpieza-datos-prueba.md` §3.3); acá no hay pedidos todavía, así que
el estado inicial correcto es `LIBRE`.

### 2.3 Los usuarios: los mismos cuatro de `SCRUM-9`, no unos nuevos

```sql
INSERT INTO usuario (nombre, rol, nombre_usuario, password_hash) VALUES
    ('Admin',     'ADMIN',    'admin',    '$2a$11$iuLT5ZWur.miWwB1iMAflua3310KofI3edAF8mSO.2rIdUxVLQwPq'),
    ('Mesera',    'MESERO',   'mesera',   '$2a$11$1sbwdoeMh2Ai.DjJUJLwcOwvSuYRN9RWg15F167AAOGhxcG1wqL8q'),
    ('Delivery',  'CAJERO',   'delivery', '$2a$11$h1S719qaOUpP4F3vocaicuBwJBhoehMCCTsxXLTfr1HZqUDGvxAnC'),
    ('Cocinera',  'COCINERO', 'cocinera', '$2a$11$hr1zcVE8Tvrvsj.vSO0fe.HSM8gASRQlr0otqQHvLQ/pPie8h1bHW');
```

Son **los mismos cuatro hashes BCrypt** que `sql/dev_renombrar_usuarios.sql` ya usa —no se
generan de nuevo—, así que la contraseña `atipico` funciona igual en la base compartida
renombrada y en cualquier ambiente nuevo levantado con este script. Un solo lugar de verdad
para esas credenciales, no dos generaciones que podrían divergir. La nota de seguridad ya
escrita en `specs/limpieza-datos-prueba.md` §5 aplica igual acá: contraseña compartida,
pensada como arranque, no como política final.

## 3. `sql/dev_datos_iniciales.sql`

Sin numerar, como el resto de los `dev_*`: no es historia de migración, es una carga de datos
para poner en marcha un ambiente que nace vacío. No requiere superusuario — todo lo que
inserta son `INSERT` simples, y `app_restaurante` tiene `INSERT` sobre las cuatro tablas
(el `BLOQUE 2` de `schema_completo.sql` ya se lo otorga).

```sql
-- =====================================================================
-- dev_datos_iniciales.sql — catálogo real y usuarios de operación para
-- un ambiente que acaba de nacer de sql/schema_completo.sql.
-- Ver specs/script-inicial-completo.md.
--
-- SIN NUMERAR A PROPÓSITO: no es esquema, es carga de datos. Corre UNA VEZ,
-- inmediatamente después de schema_completo.sql, contra una base vacía.
-- Falla si las tablas ya tienen filas (uk_mesa_numero / uk_usuario_nombre_usuario
-- lo impiden) — es la señal de que este script no es para una base que ya
-- opera, solo para una recién creada.
-- =====================================================================

\set ON_ERROR_STOP on

BEGIN;

INSERT INTO tipo_plato (nombre) VALUES
    ('Entradas'), ('Fondos'), ('Postres'), ('Bebidas');

INSERT INTO plato (nombre, precio, id_tipo_plato, estado) VALUES
    ('Causa limeña',         22.00, (SELECT id FROM tipo_plato WHERE nombre = 'Entradas'), 'DISPONIBLE'),
    ('Papa a la huancaína',  18.00, (SELECT id FROM tipo_plato WHERE nombre = 'Entradas'), 'DISPONIBLE'),
    ('Lomo saltado',         45.00, (SELECT id FROM tipo_plato WHERE nombre = 'Fondos'),   'DISPONIBLE'),
    ('Ají de gallina',       38.00, (SELECT id FROM tipo_plato WHERE nombre = 'Fondos'),   'DISPONIBLE'),
    ('Suspiro a la limeña',  16.00, (SELECT id FROM tipo_plato WHERE nombre = 'Postres'),  'DISPONIBLE'),
    ('Chicha morada jarra',  15.00, (SELECT id FROM tipo_plato WHERE nombre = 'Bebidas'),  'DISPONIBLE');

INSERT INTO mesa (numero, capacidad, estado) VALUES
    (1, 4, 'LIBRE'), (2, 2, 'LIBRE'), (3, 6, 'LIBRE'), (4, 4, 'LIBRE');

-- Mismos 4 hashes BCrypt que sql/dev_renombrar_usuarios.sql (specs/limpieza-datos-prueba.md
-- §5.1) — no se regeneran, para que "atipico" abra sesión igual en cualquier ambiente
-- levantado con este script.
INSERT INTO usuario (nombre, rol, nombre_usuario, password_hash) VALUES
    ('Admin',    'ADMIN',    'admin',    '$2a$11$iuLT5ZWur.miWwB1iMAflua3310KofI3edAF8mSO.2rIdUxVLQwPq'),
    ('Mesera',   'MESERO',   'mesera',   '$2a$11$1sbwdoeMh2Ai.DjJUJLwcOwvSuYRN9RWg15F167AAOGhxcG1wqL8q'),
    ('Delivery', 'CAJERO',   'delivery', '$2a$11$h1S719qaOUpP4F3vocaicuBwJBhoehMCCTsxXLTfr1HZqUDGvxAnC'),
    ('Cocinera', 'COCINERO', 'cocinera', '$2a$11$hr1zcVE8Tvrvsj.vSO0fe.HSM8gASRQlr0otqQHvLQ/pPie8h1bHW');

COMMIT;

\echo ''
\echo 'Listo. Ambiente inicial cargado: 4 tipos de plato, 6 platos, 4 mesas, 4 usuarios.'
\echo 'Entrar con admin / atipico (o mesera, delivery, cocinera — misma contraseña).'
```

## 4. Runbook — de una base vacía a lista para operar

```
createdb -U postgres <nombre_base>
psql -U postgres -d <nombre_base> -v ON_ERROR_STOP=1 -f sql/schema_completo.sql
psql -U postgres -d <nombre_base> -v ON_ERROR_STOP=1 -f sql/dev_datos_iniciales.sql
```

Después: `ALTER ROLE app_restaurante WITH PASSWORD '<clave>'` (`schema_completo.sql` ya
avisa esto en su propio pie) y apuntar `ConnectionStrings:DefaultConnection` a la base nueva.
No hace falta abrir un turno de caja a mano para empezar a mirar la aplicación —pero si hace
falta para crear un pedido (`sql/dev_abrir_turno.sql`, `specs/numero-pedido.md` §4.6)—, este
script no lo abre: un turno es un evento operativo del día, no parte del catálogo inicial.

## 5. Lo que no se verificó, y por qué

**No se corrió de punta a punta en un contenedor.** Docker Desktop no estaba disponible al
escribir esto. Lo que sí se verificó:

- Los nombres y tipos de columna de `tipo_plato`, `plato`, `mesa` y `usuario` contra el
  esquema real, por consulta de solo lectura (mismo método que `specs/limpieza-datos-prueba.md`
  §2).
- Que `tipo_plato`, `plato` y `mesa` son `GENERATED ALWAYS AS IDENTITY` sin necesidad de
  forzar el `id`.
- Que los 4 hashes BCrypt son los mismos ya verificados con `BCrypt.Verify()` en
  `specs/limpieza-datos-prueba.md` §5.1 — no se regeneraron, así que no hay nada nuevo que
  probar ahí.

Lo que falta probar antes de usarlo contra un ambiente real: correr `schema_completo.sql` +
`dev_datos_iniciales.sql` en secuencia sobre una base recién creada y confirmar que no tira
ningún error — el riesgo más probable es un nombre de columna o un `CHECK` que haya cambiado
entre el momento en que se escribió este spec y la versión real de `schema_completo.sql`.

## 6. Verificación

- [ ] Docker disponible; corridos en secuencia `schema_completo.sql` + `dev_datos_iniciales.sql`
      contra un contenedor limpio, sin errores.
- [ ] 4 `tipo_plato`, 6 `plato`, 4 `mesa`, 4 `usuario` — conteos exactos.
- [ ] Login funciona con `admin`/`atipico` (y las otras tres cuentas).
- [ ] `app_restaurante` puede `SELECT`/`INSERT`/`UPDATE` sobre las cuatro tablas cargadas
      (heredado del `BLOQUE 2` de `schema_completo.sql`, no de este script).
- [ ] La aplicación abre sin turno de caja activo — estado esperado, no error
      (`specs/numero-pedido.md` §8.4).

## 7. Próximo paso — fuera de alcance de este spec

Este script existe para la rama de Neon dedicada a desarrollo que `SCRUM-5` deja como
siguiente tarea. Crear esa rama, decidir si nace vacía (y por tanto necesita este script) o
clonada de la base compartida (y por tanto no lo necesita), es una decisión de esa tarea, no
de esta.
