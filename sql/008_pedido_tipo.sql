-- =====================================================================
-- 008_pedido_tipo.sql — clasifica el pedido segun donde se consume:
-- en el salon, para llevar o por delivery
-- Requiere haber corrido antes script_inicial.sql
-- Ver docs/tipo-pedido.md
-- =====================================================================

BEGIN;

-- Postgres 11+ llena las filas existentes con el DEFAULT sin reescribir la
-- tabla, asi que no hace falta el backfill en dos pasos de
-- 004_plato_estado_habilitado.sql. Todos los pedidos que ya existen fueron
-- consumidos en el salon: EN_SALON es el valor correcto para ellos, no solo
-- un relleno.
ALTER TABLE pedido
    ADD COLUMN tipo varchar(20) NOT NULL DEFAULT 'EN_SALON';

-- Espejo de Atipico.Domain.Enums.TipoPedido, en sync a mano (ver el
-- comentario de mantenimiento al inicio de script_inicial.sql).
ALTER TABLE pedido
    ADD CONSTRAINT ck_pedido_tipo CHECK (tipo IN ('EN_SALON', 'PARA_LLEVAR', 'DELIVERY'));

-- Sin indice a proposito: son tres valores sobre una tabla chica, el
-- planificador va a preferir un seq scan igual, y el filtrado de
-- Pedidos/Index.razor ocurre en memoria.

-- Sin GRANT nuevo: los permisos de app_restaurante son por tabla, no por
-- columna, y ya incluyen SELECT/INSERT/UPDATE sobre pedido.

COMMIT;
