-- =====================================================================
-- dev_abrir_turno.sql — abre un turno de caja desde psql
--
-- SIN NUMERAR A PROPÓSITO. Los scripts numerados son la historia canónica de
-- migración; este no migra nada, es una herramienta para operar la caja a mano
-- mientras el formulario de la aplicación no esté aprobado.
--
-- NO requiere superusuario. app_restaurante tiene INSERT y UPDATE sobre
-- turno_caja (sql/010_turno_caja.sql), así que corre con la misma conexión que
-- usa la aplicación.
--
-- USO
--   psql ... -v nombre='A la carta' -v cajero=juan -f sql/dev_abrir_turno.sql
--
--   nombre           el que se ve en la grilla. Por defecto 'Ejecutivo'.
--   cajero           nombre_usuario de quien abre. Por defecto, el primer
--                    CAJERO disponible (y si no hay, un ADMIN).
--   cerrar_vigente   1 para cerrar el turno abierto y abrir este en su lugar.
--                    Por defecto 0: si ya hay uno abierto, el script se planta.
--
-- PARA CERRAR SIN ABRIR OTRO no hace falta script:
--   UPDATE turno_caja SET cerrado_en = now() WHERE cerrado_en IS NULL;
-- Las dos validaciones de cierre viven en fn_turno_cierre, así que ese UPDATE
-- se rechaza solo si quedan pedidos sin cerrar o cuentas sin cobrar.
--
-- Ver specs/numero-pedido.md §4.6.
-- =====================================================================

\set ON_ERROR_STOP on

\if :{?nombre}
\else
    \set nombre Ejecutivo
\endif

\if :{?cajero}
\else
    \set cajero ''
\endif

\if :{?cerrar_vigente}
\else
    \set cerrar_vigente 0
\endif


-- ---------------------------------------------------------------------
-- 1. Quién abre
-- ---------------------------------------------------------------------
-- Se resuelve ANTES de tocar nada. Si el cajero no existe, el script tiene que
-- plantarse con el turno anterior todavía abierto: cerrarlo y recién entonces
-- fallar dejaría la caja sin ningún turno, y con eso todo pedido nuevo se
-- rechaza (RN-6).
--
-- Un CAJERO le gana a un ADMIN cuando no se pidió a nadie en particular: el
-- admin puede abrir caja, pero no es quien la abre habitualmente.
SELECT
    coalesce(u.id, 0)  AS id_cajero,
    u.id IS NOT NULL   AS hay_cajero,
    coalesce(u.nombre, '') AS nombre_cajero
FROM (SELECT 1) AS _
LEFT JOIN LATERAL (
    SELECT id, nombre
    FROM usuario
    WHERE (:'cajero' <> '' AND nombre_usuario = :'cajero')
       OR (:'cajero' =  '' AND rol IN ('CAJERO', 'ADMIN'))
    ORDER BY (rol = 'CAJERO') DESC, id
    LIMIT 1
) AS u ON true
\gset

\if :hay_cajero
\else
    \warn 'Revisá el parámetro -v cajero=<nombre_usuario>, o cargá un usuario con'
    \warn 'rol CAJERO o ADMIN. Ninguna fila fue modificada.'
    -- No alcanza con \quit: sale con código 0 y un script que no hizo nada pasaría
    -- por éxito encadenado en un &&. Esto aborta con código 3.
    DO $$ BEGIN RAISE EXCEPTION 'No se encontró el cajero indicado.'; END $$;
\endif


-- ---------------------------------------------------------------------
-- 2. ¿Ya hay uno abierto?
-- ---------------------------------------------------------------------
-- uk_turno_caja_abierto rechazaría el INSERT igual, pero con un mensaje de
-- índice único que no dice qué hacer. Esto lo detecta antes y explica la salida.
SELECT
    count(*) > 0                        AS hay_vigente,
    coalesce(max(nombre), '')           AS nombre_vigente
FROM turno_caja
WHERE cerrado_en IS NULL
\gset

\if :hay_vigente
    \if :cerrar_vigente
        \echo 'Cerrando el turno vigente:' :'nombre_vigente'
    \else
        \warn 'Ya hay un turno abierto:' :'nombre_vigente'
        \warn 'Cerralo primero, o volvé a correr esto con -v cerrar_vigente=1'
        \warn 'para cerrarlo y abrir el nuevo en una sola transacción.'
        DO $$ BEGIN RAISE EXCEPTION 'Ya hay un turno de caja abierto.'; END $$;
    \endif
\endif


-- ---------------------------------------------------------------------
-- 3. Cerrar y abrir, en una sola transacción
-- ---------------------------------------------------------------------
-- Juntas y no en dos pasos: entre el cierre y la apertura no puede haber una
-- ventana sin caja abierta, porque durante esa ventana todo pedido que entre se
-- rechaza. La numeración reinicia sola: la fila nueva nace con ultimo_numero = 0.
BEGIN;

\if :hay_vigente
    -- Acá se dispara tg_turno_cierre. Si quedan pedidos sin cerrar o cuentas sin
    -- cobrar, esto falla, ON_ERROR_STOP corta y el COMMIT nunca llega: no se
    -- cierra nada y no se abre nada.
    UPDATE turno_caja SET cerrado_en = now() WHERE cerrado_en IS NULL;
\endif

INSERT INTO turno_caja (nombre, id_cajero)
VALUES (btrim(:'nombre'), :id_cajero);

COMMIT;


-- ---------------------------------------------------------------------
-- 4. Cómo quedó
-- ---------------------------------------------------------------------
SELECT
    t.id,
    t.nombre,
    u.nombre           AS cajero,
    t.abierto_en,
    t.ultimo_numero    AS ultimo_numero_asignado
FROM turno_caja t
LEFT JOIN usuario u ON u.id = t.id_cajero
WHERE t.cerrado_en IS NULL;

\echo ''
\echo 'Turno abierto. El próximo pedido será el 001.'
