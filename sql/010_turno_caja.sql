-- =====================================================================
-- 010_turno_caja.sql — correlativo de pedido por turno de caja
-- Requiere haber corrido antes script_inicial.sql y 002..009, más
-- dev_limpieza_transaccional.sql (la tabla pedido debe estar vacía).
-- Ver docs/numero-pedido.md
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- TURNO_CAJA — una fila por apertura. Es el contador y es el período.
-- ---------------------------------------------------------------------
-- Que abrir turno INSERTE una fila, en vez de poner en cero un contador
-- único, es lo que hace verificable la unicidad del número: sin una fila
-- que represente el período no hay clave contra la cual imponer UNIQUE, y
-- después de un reinicio no quedaría forma de saber qué pedidos eran de
-- qué turno.
--
-- No hay horarios acá. El reinicio es manual, a criterio del cajero: no
-- existe corte de jornada, ni ventana de turno, ni zona horaria, ni el
-- problema de los turnos que cruzan la medianoche.
CREATE TABLE turno_caja (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    -- Texto libre: "Ejecutivo", "A la carta", o lo que el local use. Sin
    -- catálogo cerrado: el cajero abre el turno que toca y lo nombra.
    nombre         varchar(40) NOT NULL,
    -- Anulable solo por compatibilidad con procesos de soporte; todo turno
    -- abierto desde la aplicación lleva cajero.
    id_cajero      bigint,
    abierto_en     timestamptz NOT NULL DEFAULT now(),
    cerrado_en     timestamptz,
    -- Lo administra fn_pedido_numero_turno. La aplicación nunca lo escribe:
    -- en EF va como ValueGeneratedOnAddOrUpdate, igual que actualizado_en
    -- con fn_touch.
    ultimo_numero  int         NOT NULL DEFAULT 0,
    CONSTRAINT fk_turno_caja_cajero FOREIGN KEY (id_cajero)
        REFERENCES usuario (id) ON DELETE RESTRICT,
    CONSTRAINT ck_turno_caja_nombre CHECK (btrim(nombre) <> ''),
    CONSTRAINT ck_turno_caja_cierre
        CHECK (cerrado_en IS NULL OR cerrado_en >= abierto_en)
);

-- Como máximo un turno abierto a la vez. Índice único parcial sobre una
-- expresión constante: todas las filas con cerrado_en NULL colisionan
-- entre sí. Es lo que garantiza que fn_pedido_numero_turno nunca tenga que
-- elegir entre dos turnos abiertos.
CREATE UNIQUE INDEX uk_turno_caja_abierto
    ON turno_caja ((true)) WHERE cerrado_en IS NULL;

-- Para el histórico de turnos, que se lista por fecha de apertura.
CREATE INDEX ix_turno_caja_abierto_en ON turno_caja (abierto_en);


-- ---------------------------------------------------------------------
-- PEDIDO — el correlativo y su turno
-- ---------------------------------------------------------------------
-- numero_turno y no numero: mesa.numero ya existe y son cosas distintas.
--
-- Entran NOT NULL de una vez, sin el ciclo nullable -> backfill -> SET NOT
-- NULL, porque la tabla quedó vacía. Si alguien corre este script sin
-- haber corrido antes la limpieza, ADD COLUMN NOT NULL sin DEFAULT falla
-- acá mismo y la transacción se deshace entera: es la salvaguarda, no un
-- descuido.
ALTER TABLE pedido
    ADD COLUMN id_turno_caja bigint NOT NULL,
    ADD COLUMN numero_turno  int    NOT NULL;

ALTER TABLE pedido
    ADD CONSTRAINT fk_pedido_turno_caja FOREIGN KEY (id_turno_caja)
        REFERENCES turno_caja (id) ON DELETE RESTRICT,
    -- El par es único, no el número solo: cada turno reinicia en 1. El
    -- btree resultante sobre (id_turno_caja, numero_turno) es además el
    -- filtro y el orden por defecto de la grilla, así que no hace falta un
    -- índice adicional sobre id_turno_caja.
    ADD CONSTRAINT uk_pedido_numero_turno UNIQUE (id_turno_caja, numero_turno);


-- ---------------------------------------------------------------------
-- ASIGNACIÓN DEL CORRELATIVO
-- ---------------------------------------------------------------------
-- RAISE EXCEPTION sin ERRCODE deja P0001, que es el único código de
-- trigger que ApiControllerBase.TryTranslateDbError traduce: devuelve el
-- MessageText tal cual dentro de un 409. Por eso el mensaje va redactado
-- en español para el usuario final. Un ERRCODE propio caería en el
-- _ => null del switch y saldría un 500 con stack trace.
CREATE FUNCTION fn_pedido_numero_turno() RETURNS trigger AS $$
DECLARE
    v_turno bigint;
BEGIN
    -- El UPDATE toma un lock sobre la fila del turno abierto y lo mantiene
    -- hasta el commit de la transacción externa. Las dos consecuencias:
    --
    --   buscada    sin huecos, porque un rollback deshace también el
    --              incremento. Un salto de número en sala se lee como
    --              pedido perdido, y eso genera desconfianza.
    --   a respetar la transacción que crea el pedido tiene que ser corta.
    --              Medido: una que tarda 3 s en commitear bloquea 2.3 s al
    --              insert de otra conexión. Nada de impresión de comanda
    --              ni subida de comprobantes adentro.
    UPDATE turno_caja
       SET ultimo_numero = ultimo_numero + 1
     WHERE cerrado_en IS NULL
    RETURNING id, ultimo_numero INTO v_turno, NEW.numero_turno;

    IF v_turno IS NULL THEN
        RAISE EXCEPTION 'No hay un turno de caja abierto: abra uno antes de registrar pedidos.';
    END IF;

    NEW.id_turno_caja := v_turno;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tg_pedido_numero_turno
    BEFORE INSERT ON pedido
    FOR EACH ROW EXECUTE FUNCTION fn_pedido_numero_turno();


-- ---------------------------------------------------------------------
-- CIERRE DE TURNO
-- ---------------------------------------------------------------------
-- No se cierra un turno con pedidos vivos. La grilla muestra solo el turno
-- abierto, así que cerrar con mesas en curso las haría desaparecer de la
-- vista del mesero justo cuando las está atendiendo.
--
-- Se apila sobre una validación que ya existe: PedidosController impide
-- pasar un pedido a Cerrado si le quedan platos sin cobrar
-- (v_pedido_plato_sin_cobrar). Son dos niveles del mismo principio, en
-- orden: primero se cierra cada pedido, después el turno.
CREATE FUNCTION fn_turno_cierre() RETURNS trigger AS $$
DECLARE
    v_vivos int;
BEGIN
    -- Solo en la transición abierto -> cerrado. Un UPDATE sobre un turno
    -- ya cerrado no vuelve a disparar la validación.
    IF NEW.cerrado_en IS NULL OR OLD.cerrado_en IS NOT NULL THEN
        RETURN NEW;
    END IF;

    -- SERVIDO cuenta como vivo, y es el caso que justifica todo esto: el
    -- plato ya salió, la mesa está comiendo, nadie pagó. Terminales son
    -- solo CERRADO y ANULADO.
    SELECT count(*) INTO v_vivos
    FROM pedido
    WHERE id_turno_caja = OLD.id
      AND estado NOT IN ('CERRADO', 'ANULADO');

    -- El mensaje sale tal cual en la pantalla del cajero (P0001 ->
    -- TryTranslateDbError -> 409), así que concuerda en número.
    IF v_vivos = 1 THEN
        RAISE EXCEPTION 'No se puede cerrar el turno: queda 1 pedido sin cerrar.';
    ELSIF v_vivos > 1 THEN
        RAISE EXCEPTION 'No se puede cerrar el turno: quedan % pedidos sin cerrar.', v_vivos;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tg_turno_cierre
    BEFORE UPDATE ON turno_caja
    FOR EACH ROW EXECUTE FUNCTION fn_turno_cierre();


-- ---------------------------------------------------------------------
-- PERMISOS
-- ---------------------------------------------------------------------
-- ALTER DEFAULT PRIVILEGES (script_inicial.sql, BLOQUE 2) solo alcanza a
-- los objetos que cree el mismo rol que lo ejecutó. Si este script lo
-- corre otro usuario, turno_caja nace sin permisos y la aplicación falla
-- en runtime con "permission denied for table turno_caja" — un error que
-- no aparece al migrar sino al abrir el primer turno. Se otorga explícito.
--
-- Sin DELETE ni TRUNCATE, como el resto del esquema: los registros se
-- anulan, no se eliminan.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_restaurante') THEN
        EXECUTE 'GRANT SELECT, INSERT, UPDATE ON turno_caja TO app_restaurante';
        EXECUTE 'REVOKE DELETE, TRUNCATE ON turno_caja FROM app_restaurante';
    END IF;
END $$;

COMMIT;

-- Después de aplicar: la base queda SIN ningún turno abierto, así que el
-- primer INSERT en pedido falla hasta que alguien abra uno. Es el estado
-- correcto, no un error — la grilla lo contempla y muestra la acción de
-- abrir turno en lugar de una tabla vacía.
--
-- Y regenerar el snapshot:
--   pg_dump --schema-only ... > sql/schema_completo.sql
