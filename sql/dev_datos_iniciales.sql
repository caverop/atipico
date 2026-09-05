-- =====================================================================
-- dev_datos_iniciales.sql — catálogo real y usuarios de operación para
-- un ambiente que acaba de nacer de sql/schema_completo.sql.
-- Ver specs/script-inicial-completo.md.
--
-- SIN NUMERAR A PROPÓSITO: no es esquema, es carga de datos. Corre UNA VEZ,
-- inmediatamente después de schema_completo.sql, contra una base vacía.
-- Falla si las tablas ya tienen filas (uk_mesa_numero / uk_usuario_nombre_usuario
-- lo impiden) — es la señal de que este script no es para una base que ya
-- opera, solo para una recién creada.
-- =====================================================================

\set ON_ERROR_STOP on

BEGIN;

INSERT INTO tipo_plato (nombre) VALUES
    ('Entradas'), ('Fondos'), ('Postres'), ('Bebidas');

INSERT INTO plato (nombre, precio, id_tipo_plato, estado) VALUES
    ('Causa limeña',         22.00, (SELECT id FROM tipo_plato WHERE nombre = 'Entradas'), 'DISPONIBLE'),
    ('Papa a la huancaína',  18.00, (SELECT id FROM tipo_plato WHERE nombre = 'Entradas'), 'DISPONIBLE'),
    ('Lomo saltado',         45.00, (SELECT id FROM tipo_plato WHERE nombre = 'Fondos'),   'DISPONIBLE'),
    ('Ají de gallina',       38.00, (SELECT id FROM tipo_plato WHERE nombre = 'Fondos'),   'DISPONIBLE'),
    ('Suspiro a la limeña',  16.00, (SELECT id FROM tipo_plato WHERE nombre = 'Postres'),  'DISPONIBLE'),
    ('Chicha morada jarra',  15.00, (SELECT id FROM tipo_plato WHERE nombre = 'Bebidas'),  'DISPONIBLE');

INSERT INTO mesa (numero, capacidad, estado) VALUES
    (1, 4, 'LIBRE'), (2, 2, 'LIBRE'), (3, 6, 'LIBRE'), (4, 4, 'LIBRE');

-- Mismos 4 hashes BCrypt que sql/dev_renombrar_usuarios.sql (specs/limpieza-datos-prueba.md
-- §5.1) — no se regeneran, para que "atipico" abra sesión igual en cualquier ambiente
-- levantado con este script.
INSERT INTO usuario (nombre, rol, nombre_usuario, password_hash) VALUES
    ('Admin',    'ADMIN',    'admin',    '$2a$11$iuLT5ZWur.miWwB1iMAflua3310KofI3edAF8mSO.2rIdUxVLQwPq'),
    ('Mesera',   'MESERO',   'mesera',   '$2a$11$1sbwdoeMh2Ai.DjJUJLwcOwvSuYRN9RWg15F167AAOGhxcG1wqL8q'),
    ('Delivery', 'CAJERO',   'delivery', '$2a$11$h1S719qaOUpP4F3vocaicuBwJBhoehMCCTsxXLTfr1HZqUDGvxAnC'),
    ('Cocinera', 'COCINERO', 'cocinera', '$2a$11$hr1zcVE8Tvrvsj.vSO0fe.HSM8gASRQlr0otqQHvLQ/pPie8h1bHW');

COMMIT;

\echo ''
\echo 'Listo. Ambiente inicial cargado: 4 tipos de plato, 6 platos, 4 mesas, 4 usuarios.'
\echo 'Entrar con admin / atipico (o mesera, delivery, cocinera — misma contraseña).'
