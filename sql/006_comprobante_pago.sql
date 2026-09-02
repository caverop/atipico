-- =====================================================================
-- 006_comprobante_pago.sql — evidencia de los pagos por QR
--
-- El flujo es de pago adelantado: el comensal paga antes de ser atendido,
-- la cuenta nace PAGADA y la imagen del comprobante se adjunta despues.
-- Una cuenta admite VARIOS comprobantes: el primer QR puede no cubrir el
-- total por saldo insuficiente, o el monto digitarse mal y completarse con
-- un segundo pago. Cada fila respalda una parte del monto.
--
-- Por que una tabla y no una columna en cuenta: fn_cuenta_inmutable
-- rechaza todo UPDATE sobre una cuenta que ya no este ABIERTA, y una
-- columna tampoco sostendria varios comprobantes. Las dos razones son
-- independientes; ver specs/comprobantes-qr.md.
--
-- Requiere haber corrido antes script_inicial.sql (BLOQUE 1 y 2).
-- =====================================================================

BEGIN;

CREATE TABLE comprobante_pago (
    id                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_cuenta         bigint        NOT NULL,
    monto             numeric(12,2) NOT NULL,
    storage_key       text          NOT NULL,
    hash_sha256       char(64)      NOT NULL,
    tipo_contenido    varchar(30)   NOT NULL,
    bytes             integer       NOT NULL,
    id_subido_por     bigint        NOT NULL,
    creado_en         timestamptz   NOT NULL DEFAULT now(),
    -- NULL = comprobante que suma. No NULL = corrige al que apunta, y el
    -- corregido deja de sumar (ver v_comprobante_vigente).
    id_reemplaza      bigint,
    motivo_reemplazo  text,
    CONSTRAINT fk_comprobante_cuenta    FOREIGN KEY (id_cuenta)
        REFERENCES cuenta (id) ON DELETE RESTRICT,
    CONSTRAINT fk_comprobante_usuario   FOREIGN KEY (id_subido_por)
        REFERENCES usuario (id) ON DELETE RESTRICT,
    CONSTRAINT fk_comprobante_reemplaza FOREIGN KEY (id_reemplaza)
        REFERENCES comprobante_pago (id) ON DELETE RESTRICT,
    CONSTRAINT uk_comprobante_key UNIQUE (storage_key),
    -- El mismo archivo no se registra dos veces en la misma cuenta: es el
    -- control contra reusar un screenshot de pago para inflar la evidencia.
    CONSTRAINT uk_comprobante_cuenta_hash UNIQUE (id_cuenta, hash_sha256),
    -- La cadena de reemplazos no se bifurca. Los NULL siguen permitidos
    -- varias veces (UNIQUE trata cada NULL como distinto).
    CONSTRAINT uk_comprobante_reemplaza UNIQUE (id_reemplaza),
    CONSTRAINT ck_comprobante_monto CHECK (monto > 0),
    CONSTRAINT ck_comprobante_bytes CHECK (bytes > 0),
    CONSTRAINT ck_comprobante_tipo  CHECK (
        tipo_contenido IN ('image/webp','image/jpeg','image/png')),
    -- Reemplazar exige motivo, mismo criterio que motivo_anulacion en cuenta.
    CONSTRAINT ck_comprobante_reemplazo CHECK (
        id_reemplaza IS NULL
        OR (motivo_reemplazo IS NOT NULL AND btrim(motivo_reemplazo) <> ''))
);

CREATE INDEX ix_comprobante_cuenta ON comprobante_pago (id_cuenta, creado_en DESC);
CREATE INDEX ix_comprobante_hash   ON comprobante_pago (hash_sha256);

-- ---------------------------------------------------------------------
-- Inmutabilidad: mismo criterio que fn_detalle_inmutable. Un comprobante
-- es evidencia contable; se corrige registrando otro, nunca editando.
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_comprobante_inmutable() RETURNS trigger AS $$
DECLARE
    v_metodo        varchar(20);
    v_cuenta_previa bigint;
BEGIN
    IF TG_OP <> 'INSERT' THEN
        RAISE EXCEPTION 'Un comprobante no se edita ni se borra: registre uno nuevo';
    END IF;

    SELECT metodo_pago INTO v_metodo FROM cuenta WHERE id = NEW.id_cuenta;
    IF v_metodo IS DISTINCT FROM 'QR' THEN
        RAISE EXCEPTION 'La cuenta % no se pago por QR', NEW.id_cuenta;
    END IF;

    IF NEW.id_reemplaza IS NOT NULL THEN
        SELECT id_cuenta INTO v_cuenta_previa
          FROM comprobante_pago WHERE id = NEW.id_reemplaza;
        IF v_cuenta_previa IS DISTINCT FROM NEW.id_cuenta THEN
            RAISE EXCEPTION 'Un comprobante solo reemplaza a otro de la misma cuenta';
        END IF;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tg_comprobante_inmutable
    BEFORE INSERT OR UPDATE OR DELETE ON comprobante_pago
    FOR EACH ROW EXECUTE FUNCTION fn_comprobante_inmutable();

-- =====================================================================
-- VISTAS DE CONCILIACION
-- =====================================================================

-- Evidencia que sigue contando: la que nadie reemplazo.
CREATE OR REPLACE VIEW v_comprobante_vigente AS
SELECT cp.id,
       cp.id_cuenta,
       cp.monto,
       cp.storage_key,
       cp.hash_sha256,
       cp.id_subido_por,
       cp.creado_en
FROM comprobante_pago cp
WHERE NOT EXISTS (SELECT 1 FROM comprobante_pago r WHERE r.id_reemplaza = cp.id);

-- Cuentas QR cuya evidencia vigente no cubre exactamente el monto.
--
-- El filtro es estado <> 'ANULADA', no estado = 'PAGADA', y eso es deliberado:
-- en el pago adelantado el dinero entra ANTES de que la cuenta exista, y
-- PedidosController.CrearCuentaAutomaticaAsync la crea sin estado, o sea
-- ABIERTA. Filtrar por PAGADA dejaria fuera justo las cuentas del flujo que
-- este reporte vigila. Mismo criterio que v_cuenta_descuadrada.
--
-- Se usa IS DISTINCT FROM y no <: el error de digitacion puede dejar la suma
-- POR ENCIMA del total (el comensal transfirio de mas y hay que devolverle la
-- diferencia en efectivo), y ese caso tambien tiene que salir en el reporte.
-- El signo de diferencia distingue: > 0 falta evidencia, < 0 sobra dinero.
-- En una operacion sana y al dia, esta consulta no devuelve filas.
CREATE OR REPLACE VIEW v_cuenta_qr_evidencia_incompleta AS
SELECT c.id,
       c.comensal,
       c.estado,
       c.monto,
       c.pagado_en,
       c.id_mesero,
       COALESCE(SUM(v.monto), 0)           AS monto_respaldado,
       c.monto - COALESCE(SUM(v.monto), 0) AS diferencia
FROM cuenta c
LEFT JOIN v_comprobante_vigente v ON v.id_cuenta = c.id
WHERE c.estado <> 'ANULADA' AND c.metodo_pago = 'QR'
GROUP BY c.id
HAVING c.monto IS DISTINCT FROM COALESCE(SUM(v.monto), 0);

-- Un mismo archivo registrado en cuentas distintas. Dentro de una misma
-- cuenta ya lo impide uk_comprobante_cuenta_hash. En una operacion sana,
-- vacio.
CREATE OR REPLACE VIEW v_comprobante_duplicado AS
SELECT cp.id,
       cp.id_cuenta,
       cp.hash_sha256,
       cp.monto,
       cp.creado_en,
       cp.id_subido_por
FROM comprobante_pago cp
WHERE cp.hash_sha256 IN (
      SELECT hash_sha256 FROM comprobante_pago
       GROUP BY hash_sha256 HAVING count(DISTINCT id_cuenta) > 1);

-- ---------------------------------------------------------------------
-- Permisos: el ALTER DEFAULT PRIVILEGES del BLOQUE 2 solo alcanza a los
-- objetos creados por el mismo rol que lo ejecuto. Explicito para no
-- depender de con que usuario se corra esta migracion. Sin DELETE, igual
-- que el resto: aca no se borra, se reemplaza.
-- ---------------------------------------------------------------------
GRANT SELECT, INSERT ON comprobante_pago TO app_restaurante;
GRANT SELECT ON v_comprobante_vigente             TO app_restaurante;
GRANT SELECT ON v_cuenta_qr_evidencia_incompleta  TO app_restaurante;
GRANT SELECT ON v_comprobante_duplicado           TO app_restaurante;

COMMIT;
