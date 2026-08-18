-- =====================================================================
-- 005_plato_habilitado_hasta.sql — agrega fecha de fin de habilitacion
-- (opcional) a la tabla plato
-- Requiere haber corrido antes 004_plato_estado_habilitado.sql
-- =====================================================================

BEGIN;

-- NULL = sin fecha de fin, el plato queda habilitado indefinidamente desde
-- habilitado_desde. No hay backfill: los platos existentes quedan en NULL.
ALTER TABLE plato
    ADD COLUMN habilitado_hasta date;

ALTER TABLE plato
    ADD CONSTRAINT ck_plato_habilitado_rango
    CHECK (habilitado_hasta IS NULL OR habilitado_hasta >= habilitado_desde);

COMMIT;
