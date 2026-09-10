-- =====================================================================
-- 015_cuenta_metodo_qr.sql — ck_cuenta_metodo pasa a aceptar QR
-- Ver specs/reparacion-ck-cuenta-metodo.md
-- =====================================================================
BEGIN;

-- SCRUM-28: registra como migracion un cambio que se aplico a mano sobre la base y nunca
-- quedo escrito. La base desplegada acepta QR desde la feature de comprobantes
-- (specs/comprobantes-qr.md); script_inicial.sql todavia documenta YAPE/PLIN, que no
-- existen en ningun lado. Un ambiente construido desde la cadena canonica rechazaba QR y
-- con eso se caia toda la feature de comprobantes: fn_comprobante_inmutable (006) y
-- v_cuenta_qr_evidencia_incompleta (006/007) filtran por metodo_pago = 'QR'.
--
-- No angosta a los dos valores de MetodoPago.cs a proposito: la relacion correcta entre
-- enum y CHECK es subconjunto, no igualdad. Un valor de mas en la base es inofensivo, uno
-- de menos rompe produccion. Ver el spec, seccion 4.2.
--
-- En Neon esto es un no-op: el conjunto resultante es identico al vigente, asi que
-- ADD CONSTRAINT revalida la tabla y ninguna fila puede violarlo. El cambio real lo recibe
-- cualquier ambiente nuevo construido desde script_inicial.sql + migraciones numeradas.
--
-- Se conserva la rama IS NULL: metodo_pago es nullable y una cuenta abierta todavia no
-- tiene metodo. Quien exige metodo cuando el estado es PAGADA es ck_cuenta_pago, que es
-- otra restriccion y esta migracion no la toca.
--
-- DROP + ADD planos, no un bloque DO condicional: ModeloEnumsCheckTest lee esta lista con
-- una expresion regular sobre el texto del archivo, y el DDL literal es lo que sabe leer.
-- Por eso tampoco ningun comentario de arriba escribe la forma sintactica de la
-- restriccion con la lista vieja: la prueba se queda con la primera coincidencia.
ALTER TABLE cuenta DROP CONSTRAINT ck_cuenta_metodo;
ALTER TABLE cuenta
    ADD CONSTRAINT ck_cuenta_metodo
    CHECK (metodo_pago IS NULL
        OR metodo_pago IN ('EFECTIVO','TARJETA','TRANSFERENCIA','QR'));

COMMIT;
