BEGIN;

-- SCRUM-13: agrega el rol Delivery. ck_usuario_rol espeja a mano RolUsuario (ver el
-- comentario de mantenimiento en script_inicial.sql) — hay que tocar los dos.
ALTER TABLE usuario DROP CONSTRAINT ck_usuario_rol;
ALTER TABLE usuario
    ADD CONSTRAINT ck_usuario_rol
    CHECK (rol IN ('MESERO','CAJERO','COCINERO','ADMIN','DELIVERY'));

COMMIT;
