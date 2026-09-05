-- =====================================================================
-- dev_limpieza_qa_confirmada.sql — vacía los datos de prueba de la base
-- compartida entre QA y producción (specs/limpieza-datos-prueba.md).
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
-- Requiere SUPERUSUARIO (el dueño de las tablas, ej. neondb_owner):
-- app_restaurante no tiene TRUNCATE ni DELETE sobre ninguna de estas tablas.
--
-- SQL PURO — sin \set/\if/\gset de psql. Pensado para pegarse tal cual en
-- cualquier cliente (pgAdmin, DBeaver, la consola web de Neon, psql), sin
-- parámetros de línea de comandos.
--
-- CÓMO CONFIRMAR: antes de correrlo, editá la línea marcada más abajo
-- (v_confirmo) y reemplazá el texto por la frase exacta indicada ahí mismo.
-- Sin ese cambio, el script se planta en la primera sentencia y no llega a
-- tocar ninguna tabla — funciona así en cualquier cliente porque la
-- compuerta vive DENTRO de la misma transacción que el TRUNCATE: si aborta,
-- Postgres rechaza automáticamente todo lo que venga después en ese mismo
-- BEGIN/COMMIT, sin depender de que la herramienta pare al primer error.
-- =====================================================================

BEGIN;

DO $$
DECLARE
    -- <<< EDITÁ ESTA LÍNEA >>> reemplazá el texto de la derecha por:
    --     SI_VERIFIQUE_QUE_ES_TODO_PRUEBA
    -- Repetí antes la auditoría de specs/limpieza-datos-prueba.md §2 contra
    -- el estado ACTUAL de la base — no confíes en la fecha del documento.
    v_confirmo text := 'PEGAR_AQUI_LA_FRASE_DE_CONFIRMACION';
BEGIN
    IF v_confirmo <> 'SI_VERIFIQUE_QUE_ES_TODO_PRUEBA' THEN
        RAISE EXCEPTION 'Confirmación faltante o incorrecta. Editá v_confirmo en este archivo. Nada fue modificado.';
    END IF;
END $$;


-- ---------------------------------------------------------------------
-- 1. Vaciar lo transaccional
-- ---------------------------------------------------------------------
-- TRUNCATE no dispara fn_detalle_inmutable ni fn_cuenta_inmutable, que
-- rechazarían cualquier DELETE. RESTART IDENTITY: los id vuelven a 1.
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
-- (specs/limpieza-datos-prueba.md §3.3): sin este UPDATE, una mesa que quedó
-- OCUPADA por una prueba sigue OCUPADA para siempre, sin ningún pedido que
-- la explique.
UPDATE mesa SET estado = 'LIBRE' WHERE estado <> 'LIBRE';

COMMIT;

-- Los catálogos NO se tocan: usuario, plato, tipo_plato y las filas de mesa
-- quedan intactos. Ver specs/limpieza-datos-prueba.md §5.
--
-- comprobante_pago.storage_key apunta a archivos en el bucket de R2
-- compartido (atipico-comprobantes): este TRUNCATE no los borra. Capturalos
-- ANTES de correr esto si todavía no lo hiciste — specs/limpieza-datos-prueba.md §6.

-- Verificación — correr aparte, después del COMMIT de arriba:
--   SELECT (SELECT count(*) FROM pedido) AS pedidos, (SELECT count(*) FROM cuenta) AS cuentas,
--          (SELECT count(*) FROM mesa WHERE estado <> 'LIBRE') AS mesas_no_libres;
-- Las tres columnas deben dar 0.
