-- =====================================================================
-- restaurante_db — esquema base (SIN bitácora de auditoría)
-- PostgreSQL 12+ (verificado sobre 18.3)
--
-- PASO PREVIO: crear la base. CREATE DATABASE no puede ir dentro de una
-- transacción, así que se ejecuta aparte:
--
--     createdb -U postgres restaurante_db
--
--   o desde psql/pgAdmin, conectado a otra base:
--     CREATE DATABASE restaurante_db WITH ENCODING 'UTF8';
--
-- LUEGO: conectarse a restaurante_db y ejecutar este archivo completo.
--     psql -U postgres -d restaurante_db -v ON_ERROR_STOP=1 -f restaurante_db_sin_auditoria.sql
--
-- El archivo tiene DOS transacciones independientes:
--   BLOQUE 1 (esquema) — lo ejecuta el dueño del esquema.
--   BLOQUE 2 (roles)   — requiere superusuario o CREATEROLE. Opcional.
-- =====================================================================


-- #####################################################################
-- BLOQUE 1 — ESQUEMA
-- #####################################################################

BEGIN;

-- ---------------------------------------------------------------------
-- USUARIO
-- ---------------------------------------------------------------------
CREATE TABLE usuario (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre          varchar(120) NOT NULL,
    rol             varchar(20)  NOT NULL,
    activo          boolean      NOT NULL DEFAULT true,
    creado_en       timestamptz  NOT NULL DEFAULT now(),
    actualizado_en  timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_usuario_rol CHECK (rol IN ('MESERO','CAJERO','COCINERO','ADMIN'))
);

-- ---------------------------------------------------------------------
-- TIPO_PLATO
-- ---------------------------------------------------------------------
CREATE TABLE tipo_plato (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre       varchar(80) NOT NULL,
    observacion  text,
    creado_en    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uk_tipo_plato_nombre UNIQUE (nombre)
);

-- ---------------------------------------------------------------------
-- PLATO
-- ---------------------------------------------------------------------
CREATE TABLE plato (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre          varchar(120)  NOT NULL,
    precio          numeric(10,2) NOT NULL,
    activo          boolean       NOT NULL DEFAULT true,
    id_tipo_plato   bigint        NOT NULL,
    creado_en       timestamptz   NOT NULL DEFAULT now(),
    actualizado_en  timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT fk_plato_tipo FOREIGN KEY (id_tipo_plato)
        REFERENCES tipo_plato (id) ON DELETE RESTRICT,
    CONSTRAINT ck_plato_precio CHECK (precio >= 0)
);

CREATE INDEX ix_plato_tipo ON plato (id_tipo_plato);

-- ---------------------------------------------------------------------
-- MESA
-- ---------------------------------------------------------------------
CREATE TABLE mesa (
    id         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    numero     int         NOT NULL,
    capacidad  int         NOT NULL,
    estado     varchar(20) NOT NULL DEFAULT 'LIBRE',
    creado_en  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uk_mesa_numero UNIQUE (numero),
    CONSTRAINT ck_mesa_cap    CHECK (capacidad > 0),
    CONSTRAINT ck_mesa_estado CHECK (estado IN ('LIBRE','OCUPADA','RESERVADA','INACTIVA'))
);

-- ---------------------------------------------------------------------
-- PEDIDO
-- ---------------------------------------------------------------------
CREATE TABLE pedido (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    comensal        varchar(120),
    estado          varchar(20) NOT NULL DEFAULT 'ABIERTO',
    id_mesero       bigint      NOT NULL,
    creado_en       timestamptz NOT NULL DEFAULT now(),
    actualizado_en  timestamptz NOT NULL DEFAULT now(),
    cerrado_en      timestamptz,
    CONSTRAINT fk_pedido_mesero FOREIGN KEY (id_mesero)
        REFERENCES usuario (id) ON DELETE RESTRICT,
    CONSTRAINT ck_pedido_estado CHECK (estado IN ('ABIERTO','EN_PREPARACION','SERVIDO','CERRADO','ANULADO')),
    CONSTRAINT ck_pedido_cierre CHECK (estado <> 'CERRADO' OR cerrado_en IS NOT NULL)
);

CREATE INDEX ix_pedido_mesero ON pedido (id_mesero);
CREATE INDEX ix_pedido_fecha  ON pedido (creado_en);

-- ---------------------------------------------------------------------
-- PEDIDO_MESA
-- ---------------------------------------------------------------------
CREATE TABLE pedido_mesa (
    id         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_pedido  bigint      NOT NULL,
    id_mesa    bigint      NOT NULL,
    creado_en  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_pedido_mesa_pedido FOREIGN KEY (id_pedido)
        REFERENCES pedido (id) ON DELETE RESTRICT,
    CONSTRAINT fk_pedido_mesa_mesa FOREIGN KEY (id_mesa)
        REFERENCES mesa (id) ON DELETE RESTRICT,
    CONSTRAINT uk_pedido_mesa UNIQUE (id_pedido, id_mesa)
);

CREATE INDEX ix_pedido_mesa_mesa ON pedido_mesa (id_mesa);

-- ---------------------------------------------------------------------
-- PEDIDO_PLATO — una fila = una unidad de plato pedida
-- ---------------------------------------------------------------------
CREATE TABLE pedido_plato (
    id                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_pedido         bigint      NOT NULL,
    id_plato          bigint      NOT NULL,
    estado            varchar(20) NOT NULL DEFAULT 'PENDIENTE',
    creado_en         timestamptz NOT NULL DEFAULT now(),
    servido_en        timestamptz,
    anulado_en        timestamptz,
    id_anulado_por    bigint,
    motivo_anulacion  text,
    CONSTRAINT fk_pedido_plato_pedido FOREIGN KEY (id_pedido)
        REFERENCES pedido (id) ON DELETE RESTRICT,
    CONSTRAINT fk_pedido_plato_plato FOREIGN KEY (id_plato)
        REFERENCES plato (id) ON DELETE RESTRICT,
    CONSTRAINT fk_pedido_plato_anulador FOREIGN KEY (id_anulado_por)
        REFERENCES usuario (id) ON DELETE RESTRICT,
    CONSTRAINT ck_pedido_plato_estado CHECK (estado IN ('PENDIENTE','EN_PREPARACION','SERVIDO','ANULADO')),
    CONSTRAINT ck_pedido_plato_servido CHECK (estado <> 'SERVIDO' OR servido_en IS NOT NULL),
    -- anular exige responsable y motivo
    CONSTRAINT ck_pedido_plato_anulacion CHECK (
        estado <> 'ANULADO'
        OR (anulado_en IS NOT NULL
            AND id_anulado_por IS NOT NULL
            AND motivo_anulacion IS NOT NULL
            AND btrim(motivo_anulacion) <> '')
    )
);

CREATE INDEX ix_pedido_plato_pedido ON pedido_plato (id_pedido);
CREATE INDEX ix_pedido_plato_plato  ON pedido_plato (id_plato);

-- ---------------------------------------------------------------------
-- CUENTA — varias por pedido: cada comensal paga lo suyo
-- ---------------------------------------------------------------------
CREATE TABLE cuenta (
    id                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    comensal          varchar(120),
    estado            varchar(20)   NOT NULL DEFAULT 'ABIERTA',
    metodo_pago       varchar(20),
    monto             numeric(12,2) NOT NULL DEFAULT 0,
    id_mesero         bigint        NOT NULL,
    id_cajero         bigint,
    creado_en         timestamptz   NOT NULL DEFAULT now(),
    pagado_en         timestamptz,
    anulado_en        timestamptz,
    id_anulado_por    bigint,
    motivo_anulacion  text,
    CONSTRAINT fk_cuenta_mesero   FOREIGN KEY (id_mesero)
        REFERENCES usuario (id) ON DELETE RESTRICT,
    CONSTRAINT fk_cuenta_cajero   FOREIGN KEY (id_cajero)
        REFERENCES usuario (id) ON DELETE RESTRICT,
    CONSTRAINT fk_cuenta_anulador FOREIGN KEY (id_anulado_por)
        REFERENCES usuario (id) ON DELETE RESTRICT,
    CONSTRAINT ck_cuenta_estado CHECK (estado IN ('ABIERTA','PAGADA','ANULADA')),
    CONSTRAINT ck_cuenta_metodo CHECK (
        metodo_pago IS NULL
        OR metodo_pago IN ('EFECTIVO','TARJETA','TRANSFERENCIA','YAPE','PLIN')),
    CONSTRAINT ck_cuenta_monto CHECK (monto >= 0),
    -- cobrar exige método, momento y cajero identificado
    CONSTRAINT ck_cuenta_pago CHECK (
        estado <> 'PAGADA'
        OR (metodo_pago IS NOT NULL AND pagado_en IS NOT NULL AND id_cajero IS NOT NULL)),
    CONSTRAINT ck_cuenta_anulacion CHECK (
        estado <> 'ANULADA'
        OR (anulado_en IS NOT NULL
            AND id_anulado_por IS NOT NULL
            AND motivo_anulacion IS NOT NULL
            AND btrim(motivo_anulacion) <> ''))
);

CREATE INDEX ix_cuenta_mesero ON cuenta (id_mesero);
CREATE INDEX ix_cuenta_cajero ON cuenta (id_cajero);
CREATE INDEX ix_cuenta_pago   ON cuenta (pagado_en);

-- ---------------------------------------------------------------------
-- DETALLE_CUENTA
-- La UK sobre id_pedido_plato es lo que sostiene la cuenta dividida:
-- cada unidad de plato se factura a una sola cuenta, nunca dos veces.
-- ---------------------------------------------------------------------
CREATE TABLE detalle_cuenta (
    id               bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_cuenta        bigint        NOT NULL,
    id_pedido_plato  bigint        NOT NULL,
    precio_unitario  numeric(10,2) NOT NULL,
    creado_en        timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT fk_detalle_cuenta_cuenta FOREIGN KEY (id_cuenta)
        REFERENCES cuenta (id) ON DELETE RESTRICT,
    CONSTRAINT fk_detalle_cuenta_pedido_plato FOREIGN KEY (id_pedido_plato)
        REFERENCES pedido_plato (id) ON DELETE RESTRICT,
    CONSTRAINT uk_detalle_pedido_plato UNIQUE (id_pedido_plato),
    CONSTRAINT ck_detalle_precio CHECK (precio_unitario >= 0)
);

CREATE INDEX ix_detalle_cuenta_cuenta ON detalle_cuenta (id_cuenta);

-- =====================================================================
-- REGLAS DE NEGOCIO
-- Esto no es auditoría: son las reglas que impiden que un ticket ya
-- cobrado cambie, y que un plato se cobre dos veces o ninguna.
-- =====================================================================

-- Una cuenta cerrada no se modifica ni se borra. La única transición
-- admitida es la anulación documentada.
CREATE OR REPLACE FUNCTION fn_cuenta_inmutable() RETURNS trigger AS $$
BEGIN
    IF NEW.estado = 'ANULADA' AND NEW.anulado_en IS NULL THEN
        NEW.anulado_en := now();
    END IF;

    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'No se eliminan cuentas: anule la cuenta % con motivo', OLD.id;
    END IF;

    IF OLD.estado = 'ABIERTA' THEN
        RETURN NEW;
    END IF;

    IF OLD.estado = 'PAGADA' AND NEW.estado = 'ANULADA'
       AND NEW.id_anulado_por IS NOT NULL
       AND NEW.motivo_anulacion IS NOT NULL AND btrim(NEW.motivo_anulacion) <> ''
       AND (to_jsonb(NEW) - 'estado' - 'anulado_en' - 'id_anulado_por' - 'motivo_anulacion')
           IS NOT DISTINCT FROM
           (to_jsonb(OLD) - 'estado' - 'anulado_en' - 'id_anulado_por' - 'motivo_anulacion')
    THEN
        RETURN NEW;
    END IF;

    RAISE EXCEPTION 'Cuenta % en estado %: solo admite anulacion documentada', OLD.id, OLD.estado;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tg_cuenta_inmutable
    BEFORE UPDATE OR DELETE ON cuenta
    FOR EACH ROW EXECUTE FUNCTION fn_cuenta_inmutable();

-- Cubre INSERT además de UPDATE/DELETE: sin la rama de INSERT se pueden
-- agregar líneas a una cuenta ya PAGADA y el total deja de cuadrar.
CREATE OR REPLACE FUNCTION fn_detalle_inmutable() RETURNS trigger AS $$
DECLARE
    v_id_cuenta    bigint;
    v_estado       varchar(20);
    v_estado_plato varchar(20);
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'No se eliminan lineas de cuenta; anule la cuenta completa';
    END IF;

    IF TG_OP = 'UPDATE' THEN
        IF OLD.id_cuenta IS DISTINCT FROM NEW.id_cuenta THEN
            RAISE EXCEPTION 'No se puede mover una linea de cuenta a otra cuenta';
        END IF;
        v_id_cuenta := OLD.id_cuenta;
    ELSE
        v_id_cuenta := NEW.id_cuenta;
        SELECT pp.estado INTO v_estado_plato
        FROM pedido_plato pp WHERE pp.id = NEW.id_pedido_plato FOR SHARE;
        IF v_estado_plato = 'ANULADO' THEN
            RAISE EXCEPTION 'El plato % esta anulado: no puede facturarse', NEW.id_pedido_plato;
        END IF;
    END IF;

    -- FOR SHARE: impide que el cobro cierre la cuenta entre este chequeo
    -- y el COMMIT de esta transacción.
    SELECT c.estado INTO v_estado FROM cuenta c WHERE c.id = v_id_cuenta FOR SHARE;
    IF v_estado <> 'ABIERTA' THEN
        RAISE EXCEPTION 'La cuenta % no esta abierta: su detalle no se modifica', v_id_cuenta;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tg_detalle_inmutable
    BEFORE INSERT OR UPDATE OR DELETE ON detalle_cuenta
    FOR EACH ROW EXECUTE FUNCTION fn_detalle_inmutable();

-- Un plato ya facturado no se anula: se anula la cuenta.
CREATE OR REPLACE FUNCTION fn_pedido_plato_facturado() RETURNS trigger AS $$
DECLARE
    v_cuenta bigint;
BEGIN
    IF NEW.estado <> 'ANULADO' OR OLD.estado = 'ANULADO' THEN
        RETURN NEW;
    END IF;

    SELECT d.id_cuenta INTO v_cuenta FROM detalle_cuenta d WHERE d.id_pedido_plato = OLD.id;
    IF FOUND THEN
        RAISE EXCEPTION 'El plato % ya fue facturado en la cuenta %: anule la cuenta, no el plato',
            OLD.id, v_cuenta;
    END IF;

    IF NEW.anulado_en IS NULL THEN
        NEW.anulado_en := now();
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tg_pedido_plato_facturado
    BEFORE UPDATE ON pedido_plato
    FOR EACH ROW EXECUTE FUNCTION fn_pedido_plato_facturado();

-- ---------------------------------------------------------------------
-- actualizado_en
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_touch() RETURNS trigger AS $$
BEGIN
    NEW.actualizado_en := now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tg_touch_usuario BEFORE UPDATE ON usuario
    FOR EACH ROW EXECUTE FUNCTION fn_touch();
CREATE TRIGGER tg_touch_plato   BEFORE UPDATE ON plato
    FOR EACH ROW EXECUTE FUNCTION fn_touch();
CREATE TRIGGER tg_touch_pedido  BEFORE UPDATE ON pedido
    FOR EACH ROW EXECUTE FUNCTION fn_touch();

-- =====================================================================
-- VISTAS DE OPERACIÓN Y CONCILIACIÓN
-- =====================================================================

-- Qué falta cobrar de un pedido. Cerrar un pedido es válido
-- solo si no quedan filas suyas aquí.
CREATE VIEW v_pedido_plato_sin_cobrar AS
SELECT pp.id AS id_pedido_plato,
       pp.id_pedido,
       pl.nombre AS plato,
       pl.precio,
       pp.estado
FROM pedido_plato pp
JOIN plato pl ON pl.id = pp.id_plato
LEFT JOIN detalle_cuenta dc ON dc.id_pedido_plato = pp.id
WHERE dc.id IS NULL
  AND pp.estado <> 'ANULADO';

-- Cuentas cuyo monto declarado no coincide con la suma de su detalle.
-- En una base sana devuelve cero filas.
CREATE VIEW v_cuenta_descuadrada AS
SELECT c.id,
       c.estado,
       c.monto,
       COALESCE(SUM(d.precio_unitario), 0) AS suma_detalle,
       c.pagado_en
FROM cuenta c
LEFT JOIN detalle_cuenta d ON d.id_cuenta = c.id
WHERE c.estado <> 'ANULADA'
GROUP BY c.id
HAVING c.monto IS DISTINCT FROM COALESCE(SUM(d.precio_unitario), 0);

COMMIT;


-- #####################################################################
-- BLOQUE 2 — ROL DE APLICACIÓN (opcional)
-- Requiere superusuario o CREATEROLE.
-- Sin DELETE: los borrados se hacen anulando, no eliminando filas.
-- #####################################################################

BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_restaurante') THEN
        CREATE ROLE app_restaurante LOGIN;
    END IF;
END $$;

GRANT USAGE ON SCHEMA public TO app_restaurante;

GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO app_restaurante;
REVOKE DELETE, TRUNCATE ON ALL TABLES IN SCHEMA public FROM app_restaurante;

-- No se otorgan privilegios sobre secuencias: GENERATED ALWAYS AS IDENTITY
-- no los necesita, el permiso se verifica contra el INSERT de la tabla.

ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE ON TABLES TO app_restaurante;

COMMIT;

-- Asignar la contraseña aparte, fuera de este archivo:
--   ALTER ROLE app_restaurante WITH PASSWORD '<clave>';
