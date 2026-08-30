-- =====================================================================
-- 011_turno_cierre_cuentas.sql — cerrar turno exige cuentas cobradas
-- Requiere haber corrido antes 010_turno_caja.sql.
-- Ver docs/numero-pedido.md §4.7
-- =====================================================================
--
-- 010 dejaba cerrar el turno con todos los pedidos en CERRADO aunque sus
-- cuentas siguieran ABIERTA. Son dos cosas distintas y la diferencia es
-- plata: PedidosController impide pasar un pedido a Cerrado si le quedan
-- platos sin FACTURAR (v_pedido_plato_sin_cobrar), pero facturar no es
-- cobrar. Un pedido cerrado con su cuenta abierta es comida servida que
-- nadie pagó.
--
-- Cerrar caja con eso adentro es justamente lo que un cierre de turno
-- tiene que impedir.
-- =====================================================================

BEGIN;

CREATE OR REPLACE FUNCTION fn_turno_cierre() RETURNS trigger AS $$
DECLARE
    v_vivos   int;
    v_cuentas int;
BEGIN
    -- Solo en la transición abierto -> cerrado. Un UPDATE sobre un turno
    -- ya cerrado no vuelve a disparar la validación.
    IF NEW.cerrado_en IS NULL OR OLD.cerrado_en IS NOT NULL THEN
        RETURN NEW;
    END IF;

    -- 1) Pedidos sin cerrar. SERVIDO cuenta como vivo: el plato ya salió,
    --    la mesa está comiendo, nadie pagó. Terminales son solo CERRADO y
    --    ANULADO.
    SELECT count(*) INTO v_vivos
    FROM pedido
    WHERE id_turno_caja = OLD.id
      AND estado NOT IN ('CERRADO', 'ANULADO');

    IF v_vivos = 1 THEN
        RAISE EXCEPTION 'No se puede cerrar el turno: queda 1 pedido sin cerrar.';
    ELSIF v_vivos > 1 THEN
        RAISE EXCEPTION 'No se puede cerrar el turno: quedan % pedidos sin cerrar.', v_vivos;
    END IF;

    -- 2) Cuentas sin cobrar. Se cuenta DISTINCT porque una cuenta reúne
    --    varias líneas de detalle, y cada comensal paga la suya: un mismo
    --    pedido puede tener varias cuentas.
    --
    --    ANULADA no bloquea. Es un cierre deliberado y documentado
    --    (ck_cuenta_anulacion exige responsable y motivo); si bloqueara,
    --    una sola anulación dejaría el turno imposible de cerrar para
    --    siempre. Solo ABIERTA bloquea.
    SELECT count(DISTINCT c.id) INTO v_cuentas
    FROM pedido p
    JOIN pedido_plato   pp ON pp.id_pedido       = p.id
    JOIN detalle_cuenta dc ON dc.id_pedido_plato = pp.id
    JOIN cuenta         c  ON c.id               = dc.id_cuenta
    WHERE p.id_turno_caja = OLD.id
      AND c.estado = 'ABIERTA';

    -- Mensaje aparte del anterior a propósito: la acción del cajero es
    -- distinta. "Sin cerrar" lo manda a cerrar pedidos; "sin cobrar", a
    -- cobrar. Un mensaje único lo mandaría al lugar equivocado.
    IF v_cuentas = 1 THEN
        RAISE EXCEPTION 'No se puede cerrar el turno: queda 1 cuenta sin cobrar.';
    ELSIF v_cuentas > 1 THEN
        RAISE EXCEPTION 'No se puede cerrar el turno: quedan % cuentas sin cobrar.', v_cuentas;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMIT;

-- El trigger tg_turno_cierre creado en 010 no se toca: sigue apuntando a
-- esta misma función, ahora con el chequeo agregado.
--
-- Nota sobre el orden de los dos chequeos: primero pedidos, después
-- cuentas. Un pedido sin cerrar casi siempre arrastra su cuenta abierta,
-- así que avisar por la cuenta antes que por el pedido mandaría al cajero
-- a cobrar algo que la mesa todavía está comiendo.
