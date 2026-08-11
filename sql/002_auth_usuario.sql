-- =====================================================================
-- 002_auth_usuario.sql — agrega credenciales de login a la tabla usuario
-- Requiere haber corrido antes restaurante_db_sin_auditoria.sql
-- =====================================================================

BEGIN;

ALTER TABLE usuario
    ADD COLUMN nombre_usuario varchar(60),
    ADD COLUMN password_hash varchar(200);

-- Backfill para filas ya existentes: nombre_usuario derivado del nombre
-- (nombre.apellido.id, garantiza unicidad) y una contraseña temporal
-- 'Atipico123!' que cada usuario debe cambiar en su primer login.
-- Hash generado con BCrypt (work factor 11) para esa contraseña exacta.
UPDATE usuario
SET nombre_usuario = lower(regexp_replace(trim(nombre), '\s+', '.', 'g')) || '.' || id,
    password_hash = '$2a$11$T5BIgb3oaJJKnHIwihW.Du5ca4iSuw9Q47SK5HZ8wMM71qX8IeeDG'
WHERE nombre_usuario IS NULL;

ALTER TABLE usuario
    ALTER COLUMN nombre_usuario SET NOT NULL,
    ALTER COLUMN password_hash SET NOT NULL;

ALTER TABLE usuario
    ADD CONSTRAINT uk_usuario_nombre_usuario UNIQUE (nombre_usuario);

COMMIT;
