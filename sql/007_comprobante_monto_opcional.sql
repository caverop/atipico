-- =====================================================================
-- 007_comprobante_monto_opcional.sql — el monto del comprobante lo
-- extraera el OCR, no el mesero
--
-- Cambia el significado de comprobante_pago.monto: pasa de "cuanto
-- respalda, declarado a mano" a "cuanto respalda, extraido de la imagen",
-- y NULL pasa a significar "todavia no se determino". Nadie teclea ese
-- importe: la cifra ya esta impresa en el comprobante y transcribirla en
-- el mostrador es justo la friccion que este flujo evita.
--
-- La conciliacion no se debilita, se escalona: hasta que el OCR llene
-- montos, el reporte vigila que ninguna cuenta QR se quede sin evidencia;
-- cuando empiece a llenarlos, la segunda rama del HAVING se activa sola.
--
-- Requiere haber corrido antes 006_comprobante_pago.sql
-- =====================================================================

BEGIN;

ALTER TABLE comprobante_pago
    ALTER COLUMN monto DROP NOT NULL;

ALTER TABLE comprobante_pago
    DROP CONSTRAINT ck_comprobante_monto;

ALTER TABLE comprobante_pago
    ADD CONSTRAINT ck_comprobante_monto CHECK (monto IS NULL OR monto > 0);

-- CREATE OR REPLACE VIEW no sirve aca: solo admite agregar columnas al
-- final, y esta vista cambia su lista (entran comprobantes y sin_monto,
-- sale diferencia). Hay que soltarla y volver a crearla — lo que tambien
-- se lleva sus GRANT, por eso se reponen abajo.
DROP VIEW IF EXISTS v_cuenta_qr_evidencia_incompleta;

-- Dos ramas, y hoy solo la primera hace trabajo:
--   count(v.id) = 0                -> cuenta QR sin ninguna evidencia
--   sin_monto = 0 AND monto <> suma -> evidencia que no cubre el cobro
--
-- Mientras los montos sean NULL la segunda nunca se cumple y esto es una
-- lista de cuentas sin comprobante. Cuando el OCR los llene, la misma
-- vista pasa a exigir que la evidencia cuadre, sin tocar una linea.
--
-- La condicion sin_monto = 0 evita el falso positivo obvio: una cuenta con
-- dos comprobantes de los que solo uno tiene monto extraido no esta
-- descuadrada, esta a medio procesar.
CREATE VIEW v_cuenta_qr_evidencia_incompleta AS
SELECT c.id,
       c.comensal,
       c.estado,
       c.monto,
       c.pagado_en,
       c.id_mesero,
       count(v.id)                                AS comprobantes,
       count(v.id) FILTER (WHERE v.monto IS NULL) AS sin_monto,
       COALESCE(SUM(v.monto), 0)                  AS monto_respaldado
FROM cuenta c
LEFT JOIN v_comprobante_vigente v ON v.id_cuenta = c.id
WHERE c.estado <> 'ANULADA' AND c.metodo_pago = 'QR'
GROUP BY c.id
HAVING count(v.id) = 0
    OR (count(v.id) FILTER (WHERE v.monto IS NULL) = 0
        AND c.monto IS DISTINCT FROM COALESCE(SUM(v.monto), 0));

GRANT SELECT ON v_cuenta_qr_evidencia_incompleta TO app_restaurante;

COMMIT;
