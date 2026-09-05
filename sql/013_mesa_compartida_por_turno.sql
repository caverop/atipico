-- =====================================================================
-- 013_mesa_compartida_por_turno.sql — una mesa vuelve a poder compartirse
-- Requiere haber corrido antes 012_pedido_unicidad_por_turno.sql.
-- Ver specs/mesa-compartida-por-turno.md
-- =====================================================================
BEGIN;

-- SCRUM-17: revierte la mitad de sql/012_pedido_unicidad_por_turno.sql que trataba a la mesa
-- (RN-14, specs/numero-pedido.md §4.10). La regla hermana sobre el comensal (RN-13,
-- uk_pedido_comensal_activo) NO se toca: el ticket pide solo la mesa. Ver
-- specs/mesa-compartida-por-turno.md.
--
-- El caso que bloqueaba de más: dos pedidos activos (p.ej. dos grupos distintos, o platos
-- pedidos en momentos separados) sentados sobre la misma mesa grande a la vez. Es un uso
-- legítimo del salón, y Mesa.Estado ya lo modela bien sin asumir un único pedido activo por
-- mesa (ver specs/mesa-compartida-por-turno.md §3): no hace falta reemplazar la regla por
-- otra, alcanza con quitarla.
DROP TRIGGER tg_pedido_mesa_ocupada ON pedido_mesa;
DROP FUNCTION fn_pedido_mesa_ocupada();

COMMIT;
