-- =====================================================================
-- 009_pedido_direccion_entrega.sql — a donde va un pedido DELIVERY
-- Requiere haber corrido antes script_inicial.sql y 008_pedido_tipo.sql
-- Ver specs/direccion-entrega.md
-- =====================================================================

BEGIN;

-- Las cuatro anulables: un pedido EN_SALON o PARA_LLEVAR las deja vacias, y
-- uno DELIVERY puede nacer sin ellas (se avisa en la interfaz, no se bloquea:
-- ver §4 del spec). ADD COLUMN sin DEFAULT no reescribe la tabla.
--
--   direccion_entrega     la referencia escrita ("casa verde, media cuadra
--                         del surtidor"), que es lo unico capaz de
--                         contradecir al pin cuando el punto esta mal
--   ubicacion_compartida  lo que llego de WhatsApp TAL CUAL, se haya podido
--                         parsear o no. Es lo que permite aceptar un enlace
--                         corto maps.app.goo.gl sin resolverlo: el
--                         repartidor lo abre igual
ALTER TABLE pedido
    ADD COLUMN direccion_entrega    text,
    ADD COLUMN ubicacion_compartida text,
    ADD COLUMN latitud_entrega      numeric(9,6),
    ADD COLUMN longitud_entrega     numeric(9,6);

-- Una latitud sin longitud no es media ubicacion: no es nada.
ALTER TABLE pedido
    ADD CONSTRAINT ck_pedido_coordenada
        CHECK ((latitud_entrega IS NULL) = (longitud_entrega IS NULL));

-- Rangos del sistema de coordenadas. Atajan basura pegada, no el error de
-- invertir lat/lng: ese se avisa en la interfaz con la caja de Bolivia,
-- porque una caja mal calibrada bloqueando pedidos reales seria peor que el
-- error que evita.
ALTER TABLE pedido
    ADD CONSTRAINT ck_pedido_latitud
        CHECK (latitud_entrega IS NULL OR latitud_entrega BETWEEN -90 AND 90),
    ADD CONSTRAINT ck_pedido_longitud
        CHECK (longitud_entrega IS NULL OR longitud_entrega BETWEEN -180 AND 180);

-- Sin indice: no se busca por coordenada.
-- Sin GRANT nuevo: los permisos de app_restaurante son por tabla, no por
-- columna, y ya incluyen UPDATE sobre pedido.

COMMIT;
