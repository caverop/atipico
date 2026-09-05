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
--
-- USO
--   psql <conexión> -v confirmo=SI_VERIFIQUE_QUE_ES_TODO_PRUEBA \
--        -f sql/dev_limpieza_qa_confirmada.sql
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

-- \if no admite comparaciones de igualdad directamente: con
-- ":{'var'} = 'literal'" psql tira "se esperaba booleano" SIEMPRE, coincida o
-- no el valor (probado). El patrón correcto, ya usado en dev_abrir_turno.sql,
-- es resolver la comparación con un SELECT y recién testear el booleano.
SELECT :'confirmo' = 'SI_VERIFIQUE_QUE_ES_TODO_PRUEBA' AS confirmado
\gset

\if :confirmado
\else
    \warn 'Este script vacía datos en la base COMPARTIDA de QA y producción.'
    \warn 'Antes de correrlo, repetí la auditoría de specs/limpieza-datos-prueba.md §2'
    \warn 'contra el estado ACTUAL de la base — no contra esta fecha.'
    \warn 'Si sigue siendo 100% prueba, corré con:'
    \warn '  -v confirmo=SI_VERIFIQUE_QUE_ES_TODO_PRUEBA'
    DO $$ BEGIN RAISE EXCEPTION 'Confirmación faltante o incorrecta. Nada fue modificado.'; END $$;
\endif


-- ---------------------------------------------------------------------
-- 1. Vaciar lo transaccional
-- ---------------------------------------------------------------------
-- Mismo mecanismo y misma lista que dev_limpieza_transaccional.sql: TRUNCATE
-- no dispara fn_detalle_inmutable ni fn_cuenta_inmutable, que rechazarían
-- cualquier DELETE. RESTART IDENTITY: los id vuelven a 1.
--
-- turno_caja se agrega a la lista: el script original no lo tenía porque
-- 010_turno_caja.sql llegó después. Vaciarlo es parte de "sin datos de
-- prueba" — el único turno que hay hoy lo abrió test.cajero.
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
-- (specs/limpieza-datos-prueba.md §3.3): sin este UPDATE, una mesa que quedó
-- OCUPADA por una prueba sigue OCUPADA para siempre, sin ningún pedido que
-- la explique. Verificado: 3 de las 4 mesas de hoy están OCUPADA sin ninguna
-- fila en pedido_mesa que las respalde.
UPDATE mesa SET estado = 'LIBRE' WHERE estado <> 'LIBRE';

COMMIT;

-- Los catálogos NO se tocan: usuario, plato, tipo_plato y las filas de mesa
-- quedan intactos. Ver specs/limpieza-datos-prueba.md §5 — es una decisión
-- que se dejó explícita y abierta, no un descuido.
--
-- comprobante_pago.storage_key apunta a archivos en el bucket de R2
-- compartido (atipico-comprobantes, specs/cicd-github-azure-render.md §3.3):
-- este TRUNCATE no los borra. Capturalos ANTES de correr esto — ver
-- specs/limpieza-datos-prueba.md §6.

\echo ''
\echo 'Listo. Verificá con:'
\echo '  SELECT (SELECT count(*) FROM pedido) AS pedidos, (SELECT count(*) FROM cuenta) AS cuentas,'
\echo '         (SELECT count(*) FROM mesa WHERE estado <> ''LIBRE'') AS mesas_no_libres;'
\echo 'Las tres columnas deben dar 0.'
