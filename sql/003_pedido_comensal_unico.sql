-- =====================================================================
-- 003_pedido_comensal_unico.sql — evita comensales duplicados entre
-- pedidos activos (Abierto / En preparación)
-- Requiere haber corrido antes script_inicial.sql
-- =====================================================================

BEGIN;

-- Indice unico parcial: dos pedidos activos (ABIERTO o EN_PREPARACION) no
-- pueden compartir el mismo comensal. Los pedidos sin comensal (NULL) o ya
-- Servido/Cerrado/Anulado quedan fuera de la restriccion; Postgres no
-- considera iguales dos NULL en un indice unico, asi que varios pedidos
-- activos sin comensal siguen siendo validos.
CREATE UNIQUE INDEX uk_pedido_comensal_activo
    ON pedido (comensal)
    WHERE estado IN ('ABIERTO', 'EN_PREPARACION');

COMMIT;
