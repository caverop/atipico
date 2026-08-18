-- =====================================================================
-- 004_plato_estado_habilitado.sql — agrega estado del ciclo de vida del
-- menu y fecha de habilitacion a la tabla plato
-- Requiere haber corrido antes script_inicial.sql
-- =====================================================================

BEGIN;

ALTER TABLE plato
    ADD COLUMN estado           varchar(20),
    ADD COLUMN habilitado_desde date;

-- Backfill para platos existentes: Disponible desde su fecha de creacion.
UPDATE plato
SET estado = 'DISPONIBLE',
    habilitado_desde = creado_en::date
WHERE estado IS NULL;

ALTER TABLE plato
    ALTER COLUMN estado SET NOT NULL,
    ALTER COLUMN estado SET DEFAULT 'DISPONIBLE',
    ALTER COLUMN habilitado_desde SET NOT NULL,
    ALTER COLUMN habilitado_desde SET DEFAULT CURRENT_DATE;

-- Espejo de ck_pedido_plato_estado / UpperSnakeCaseEnumConverter (ver el
-- comentario de mantenimiento al inicio de script_inicial.sql): valores en
-- sync a mano con Atipico.Domain.Enums.EstadoPlato.
ALTER TABLE plato
    ADD CONSTRAINT ck_plato_estado CHECK (estado IN ('DISPONIBLE', 'AGOTADO', 'DESCONTINUADO'));

COMMIT;
