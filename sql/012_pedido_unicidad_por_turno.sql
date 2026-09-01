-- =====================================================================
-- 012_pedido_unicidad_por_turno.sql — comensal y mesa, únicos por turno
-- Requiere haber corrido antes 010_turno_caja.sql.
-- Ver specs/numero-pedido.md §4.10
-- =====================================================================
--
-- Dos reglas hermanas, con la misma forma: dentro de un turno, ni el nombre
-- del comensal ni la mesa pueden estar en dos pedidos vivos a la vez.
--
-- "Vivos" y no "todos los del turno", a propósito. Una mesa se ocupa y se
-- libera varias veces por turno, y pedido_mesa no se puede borrar
-- (app_restaurante no tiene GRANT DELETE): con la regla estricta, la primera
-- mesa usada quedaría bloqueada hasta el cierre de caja y sin forma de
-- soltarla. Lo mismo con un nombre repetido a lo largo del turno.
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- COMENSAL — ahora por turno
-- ---------------------------------------------------------------------
-- 003_pedido_comensal_unico.sql lo impuso globalmente. Se reemplaza el índice
-- conservando el NOMBRE: es el que llega en PostgresException.ConstraintName y
-- el que ApiControllerBase.DescribirRestriccion traduce al español.
--
-- Hoy el alcance nuevo y el viejo coinciden, y conviene saber por qué antes de
-- creer que este bloque no hace nada: un pedido ABIERTO o EN_PREPARACION impide
-- cerrar su turno (fn_turno_cierre), así que todos los pedidos activos del
-- sistema están siempre en el único turno abierto. La diferencia aparece el día
-- que esa regla se relaje —o que alguien cierre un turno por SQL a mano, como
-- pasa en desarrollo—: ahí el índice global empezaría a rechazar nombres de
-- turnos ya terminados, y este no.
--
-- Los NULL siguen exentos: Postgres no considera iguales dos NULL en un índice
-- único, así que varios pedidos activos sin comensal siguen siendo válidos.
DROP INDEX uk_pedido_comensal_activo;

CREATE UNIQUE INDEX uk_pedido_comensal_activo
    ON pedido (id_turno_caja, comensal)
    WHERE estado IN ('ABIERTO', 'EN_PREPARACION');


-- ---------------------------------------------------------------------
-- MESA — una mesa, un pedido vivo por turno
-- ---------------------------------------------------------------------
-- Antes no existía: uk_pedido_mesa solo impide asociar la misma mesa DOS VECES
-- AL MISMO pedido. Dos pedidos distintos podían sentarse en la mesa 5 a la vez,
-- y la interfaz se limitaba a avisar "ya ocupada por otro pedido" dejando
-- pasar igual.
--
-- No puede ser un índice único parcial: el predicado necesita pedido.estado y
-- pedido.id_turno_caja, y un índice sobre pedido_mesa solo puede mirar columnas
-- de pedido_mesa. Va como trigger.
--
-- SERVIDO cuenta como ocupada, y acá se separa del comensal: servido el plato,
-- el nombre ya cumplió su función y se puede reusar, pero la mesa sigue con
-- gente comiendo hasta que el pedido cierra. Terminales son solo CERRADO y
-- ANULADO, igual que en fn_turno_cierre.
CREATE FUNCTION fn_pedido_mesa_ocupada() RETURNS trigger AS $$
DECLARE
    v_turno  bigint;
    v_numero int;
    v_otro   int;
BEGIN
    SELECT id_turno_caja INTO v_turno FROM pedido WHERE id = NEW.id_pedido;

    -- Lock sobre la fila de la mesa, sostenido hasta el commit. Sin esto, dos
    -- meseros sentando gente en la mesa 5 al mismo tiempo pasan los dos el
    -- chequeo y la doble ocupación entra igual: el conteo de abajo no ve una
    -- fila que otra transacción todavía no commiteó. Es el mismo recurso que
    -- usa fn_pedido_numero_turno, y con la misma condición: la transacción que
    -- asocia la mesa tiene que ser corta.
    SELECT numero INTO v_numero FROM mesa WHERE id = NEW.id_mesa FOR UPDATE;

    SELECT count(*) INTO v_otro
    FROM pedido_mesa pm
    JOIN pedido p ON p.id = pm.id_pedido
    WHERE pm.id_mesa    = NEW.id_mesa
      AND pm.id_pedido <> NEW.id_pedido
      AND p.id_turno_caja = v_turno
      AND p.estado NOT IN ('CERRADO', 'ANULADO');

    -- RAISE EXCEPTION sin ERRCODE deja P0001, el único código de trigger que
    -- TryTranslateDbError traduce: devuelve el MessageText tal cual dentro de un
    -- 409. Por eso el texto va redactado para el mesero, con el número de mesa
    -- que él ve en el salón y no con el id.
    IF v_otro > 0 THEN
        RAISE EXCEPTION 'La mesa % ya está ocupada por otro pedido de este turno.', v_numero;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- También en UPDATE: pedido_mesa se expone por el CRUD genérico, así que un PUT
-- puede mover la fila a otra mesa sin pasar por el INSERT.
CREATE TRIGGER tg_pedido_mesa_ocupada
    BEFORE INSERT OR UPDATE ON pedido_mesa
    FOR EACH ROW EXECUTE FUNCTION fn_pedido_mesa_ocupada();

COMMIT;
