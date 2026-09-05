-- =====================================================================
-- dev_renombrar_usuarios.sql — reemplaza los 4 usuarios de prueba por
-- cuentas de operación, mismo rol, mismo id.
-- Ver specs/limpieza-datos-prueba.md §5.
--
-- SIN NUMERAR A PROPÓSITO: no cambia esquema, es una carga de datos de una
-- sola vez. NO requiere superusuario — app_restaurante tiene UPDATE sobre
-- usuario.
--
-- Los cuatro hashes son BCrypt real para la contraseña "atipico", generados
-- con BCrypt.Net-Next 4.1.0 (la misma versión exacta que usa
-- BCryptPasswordHasher.Hash() en Atipico.Infraestructure) y verificados con
-- BCrypt.Verify() en el mismo proceso que los generó, antes de escribirlos
-- acá. No son valores de ejemplo.
-- =====================================================================

\set ON_ERROR_STOP on

BEGIN;

UPDATE usuario SET
    nombre = 'Admin',
    nombre_usuario = 'admin',
    password_hash = '$2a$11$iuLT5ZWur.miWwB1iMAflua3310KofI3edAF8mSO.2rIdUxVLQwPq'
WHERE nombre_usuario = 'test.admin';

UPDATE usuario SET
    nombre = 'Mesera',
    nombre_usuario = 'mesera',
    password_hash = '$2a$11$1sbwdoeMh2Ai.DjJUJLwcOwvSuYRN9RWg15F167AAOGhxcG1wqL8q'
WHERE nombre_usuario = 'test.mesero';

-- Ocupa el lugar de test.cajero, mismo rol CAJERO. "Delivery" es un tipo de
-- pedido (ck_pedido_tipo), no un rol de usuario: el sistema solo tiene
-- Mesero/Cajero/Cocinero/Admin. Confirmado con el usuario antes de escribir
-- esto — ver specs/limpieza-datos-prueba.md §5.
UPDATE usuario SET
    nombre = 'Delivery',
    nombre_usuario = 'delivery',
    password_hash = '$2a$11$h1S719qaOUpP4F3vocaicuBwJBhoehMCCTsxXLTfr1HZqUDGvxAnC'
WHERE nombre_usuario = 'test.cajero';

UPDATE usuario SET
    nombre = 'Cocinera',
    nombre_usuario = 'cocinera',
    password_hash = '$2a$11$hr1zcVE8Tvrvsj.vSO0fe.HSM8gASRQlr0otqQHvLQ/pPie8h1bHW'
WHERE nombre_usuario = 'test.cocinero';

COMMIT;

\echo ''
\echo 'Listo. Verificá con:'
\echo '  SELECT id, nombre, nombre_usuario, rol FROM usuario ORDER BY id;'
\echo 'Las cuatro filas deben mostrar admin/mesera/delivery/cocinera, con el rol de siempre.'
