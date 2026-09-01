-- =====================================================================
-- dev_limpieza_transaccional.sql — vacía los datos transaccionales
--
-- SIN NUMERAR A PROPÓSITO. Los scripts numerados son la historia canónica
-- de migración y se corren en todos los entornos; este no. Es una
-- herramienta de desarrollo y NUNCA debe ejecutarse contra datos reales.
--
-- Requiere SUPERUSUARIO. El rol app_restaurante no tiene TRUNCATE
-- (script_inicial.sql, BLOQUE 2), justamente para que la aplicación no
-- pueda hacer esto ni por accidente.
--
-- Por qué hace falta: 010_turno_caja.sql agrega pedido.id_turno_caja y
-- pedido.numero_turno como NOT NULL, y no hay forma honesta de inventarle
-- un turno de caja a los pedidos que ya existen. Fabricar turnos que nunca
-- ocurrieron sería el único lugar del sistema donde se inventa historia
-- operativa, en un esquema construido sobre lo contrario.
-- Ver specs/numero-pedido.md §9.
-- =====================================================================

BEGIN;

-- El CASCADE haría innecesario listarlas todas, pero se listan igual: así
-- queda escrito qué se pierde, en vez de depender de lo que CASCADE
-- arrastre por su cuenta.
--
-- DELETE no es alternativa, y no por costumbre: fn_detalle_inmutable y
-- fn_cuenta_inmutable lanzan excepción ante cualquier DELETE con filas, y
-- todas las FK son ON DELETE RESTRICT. TRUNCATE no dispara triggers de
-- fila, que es exactamente por lo que sirve acá.
--
-- RESTART IDENTITY: los id vuelven a empezar en 1.
TRUNCATE
    comprobante_pago,
    detalle_cuenta,
    cuenta,
    pedido_plato,
    pedido_mesa,
    pedido
    RESTART IDENTITY CASCADE;

COMMIT;

-- Los catálogos NO se tocan: usuario, plato, tipo_plato y mesa quedan
-- intactos, así que no hay que volver a sembrar ni recrear los usuarios
-- para poder entrar a la aplicación.
--
-- Queda fuera del alcance de este script: comprobante_pago.storage_key
-- apunta a archivos en R2 que esto no borra. En desarrollo quedan como
-- huérfanos inofensivos; si alguna vez se limpia un entorno con volumen
-- real, hay que barrer el bucket aparte.
