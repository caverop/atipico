-- =====================================================================
-- schema_completo.sql — snapshot consolidado del esquema de Atipico
-- PostgreSQL 12+ (verificado sobre 18.3)
--
-- REGENERADO el 2026-08-31 con pg_dump --schema-only sobre una base
-- descartable construida así: el snapshot anterior (que reflejaba la base
-- real hasta 005_plato_habilitado_hasta.sql) + las migraciones 006 a 012
-- aplicadas en orden. Las 7 aplicaron limpias.
--
-- Reemplaza al snapshot del 2026-08-18, que se había quedado 7 migraciones
-- atrás: le faltaban comprobante_pago, turno_caja, pedido.tipo,
-- pedido.direccion_entrega, pedido.numero_turno y pedido.id_turno_caja.
--
-- Contiene 11 tablas, 15 índices, 8 funciones, 10 triggers y 5 vistas.
--
-- Este archivo NO reemplaza a script_inicial.sql ni a las migraciones
-- numeradas: esas siguen siendo el historial canónico, y script_inicial.sql
-- no se edita una vez aplicado. Es un atajo de un solo archivo para levantar
-- un ambiente nuevo sin correr trece archivos en secuencia.
--
-- FORMATO: el cuerpo es salida literal de pg_dump, así que los CHECK de lista
-- aparecen como `= ANY (ARRAY[...])` y no como `IN (...)`. Es la forma en que
-- PostgreSQL los tiene almacenados. ModeloEnumsCheckTest lee las dos formas.
--
-- BLOQUE 2 (rol de aplicación) va al final, escrito a mano: pg_dump no emite
-- CREATE ROLE porque los roles son de cluster, no de base. Es idempotente.
-- La app se conecta como app_restaurante, no como el superusuario. La
-- contraseña se fija aparte con ALTER ROLE app_restaurante WITH PASSWORD.
--
-- MANTENIMIENTO — enums de C# vs. CHECK constraints:
-- Cada CHECK de lista duplica, a mano, un enum de Atipico.Domain/Enums/*.cs
-- (mapeado a UPPER_SNAKE_CASE por UpperSnakeCaseEnumConverter). No hay una
-- única fuente de verdad — pero desde 2026-08-31 sí hay una prueba que falla
-- si divergen: Atipico.Infraestructure.Tests/ModeloEnumsCheckTest.cs.
--   ck_usuario_rol         <-> RolUsuario
--   ck_mesa_estado         <-> EstadoMesa
--   ck_pedido_estado       <-> EstadoPedido
--   ck_pedido_plato_estado <-> EstadoPedidoPlato
--   ck_cuenta_estado       <-> EstadoCuenta
--   ck_plato_estado        <-> EstadoPlato (004_plato_estado_habilitado.sql)
--   ck_pedido_tipo         <-> TipoPedido  (008_pedido_tipo.sql)
--   ck_cuenta_metodo       <-> MetodoPago — DRIFT CONOCIDO: la base permite
--       ('EFECTIVO','TARJETA','TRANSFERENCIA','QR') y MetodoPago.cs define
--       solo Efectivo/Qr. Es holgura deliberada. OJO: script_inicial.sql
--       todavía documenta ('...','YAPE','PLIN'), que NO es lo desplegado y no
--       hay migración que registre el cambio. Este archivo manda.
-- =====================================================================

--
-- PostgreSQL database dump
--

\restrict q7QXGFjmQg3ZF9PgUUSabYPzVKemq4RcDnoyT1dv5pzmiQzcC9E4z3mpU5h1Q1E

-- Dumped from database version 18.3
-- Dumped by pg_dump version 18.3

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: fn_comprobante_inmutable(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_comprobante_inmutable() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_metodo        varchar(20);
    v_cuenta_previa bigint;
BEGIN
    IF TG_OP <> 'INSERT' THEN
        RAISE EXCEPTION 'Un comprobante no se edita ni se borra: registre uno nuevo';
    END IF;

    SELECT metodo_pago INTO v_metodo FROM cuenta WHERE id = NEW.id_cuenta;
    IF v_metodo IS DISTINCT FROM 'QR' THEN
        RAISE EXCEPTION 'La cuenta % no se pago por QR', NEW.id_cuenta;
    END IF;

    IF NEW.id_reemplaza IS NOT NULL THEN
        SELECT id_cuenta INTO v_cuenta_previa
          FROM comprobante_pago WHERE id = NEW.id_reemplaza;
        IF v_cuenta_previa IS DISTINCT FROM NEW.id_cuenta THEN
            RAISE EXCEPTION 'Un comprobante solo reemplaza a otro de la misma cuenta';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;


--
-- Name: fn_cuenta_inmutable(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_cuenta_inmutable() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW.estado = 'ANULADA' AND NEW.anulado_en IS NULL THEN
        NEW.anulado_en := now();
    END IF;

    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'No se eliminan cuentas: anule la cuenta % con motivo', OLD.id;
    END IF;

    IF OLD.estado = 'ABIERTA' THEN
        RETURN NEW;
    END IF;

    IF OLD.estado = 'PAGADA' AND NEW.estado = 'ANULADA'
       AND NEW.id_anulado_por IS NOT NULL
       AND NEW.motivo_anulacion IS NOT NULL AND btrim(NEW.motivo_anulacion) <> ''
       AND (to_jsonb(NEW) - 'estado' - 'anulado_en' - 'id_anulado_por' - 'motivo_anulacion')
           IS NOT DISTINCT FROM
           (to_jsonb(OLD) - 'estado' - 'anulado_en' - 'id_anulado_por' - 'motivo_anulacion')
    THEN
        RETURN NEW;
    END IF;

    RAISE EXCEPTION 'Cuenta % en estado %: solo admite anulacion documentada', OLD.id, OLD.estado;
END;
$$;


--
-- Name: fn_detalle_inmutable(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_detalle_inmutable() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_id_cuenta    bigint;
    v_estado       varchar(20);
    v_estado_plato varchar(20);
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'No se eliminan lineas de cuenta; anule la cuenta completa';
    END IF;

    IF TG_OP = 'UPDATE' THEN
        IF OLD.id_cuenta IS DISTINCT FROM NEW.id_cuenta THEN
            RAISE EXCEPTION 'No se puede mover una linea de cuenta a otra cuenta';
        END IF;
        v_id_cuenta := OLD.id_cuenta;
    ELSE
        v_id_cuenta := NEW.id_cuenta;
        SELECT pp.estado INTO v_estado_plato
        FROM pedido_plato pp WHERE pp.id = NEW.id_pedido_plato FOR SHARE;
        IF v_estado_plato = 'ANULADO' THEN
            RAISE EXCEPTION 'El plato % esta anulado: no puede facturarse', NEW.id_pedido_plato;
        END IF;
    END IF;

    -- FOR SHARE: impide que el cobro cierre la cuenta entre este chequeo
    -- y el COMMIT de esta transacción.
    SELECT c.estado INTO v_estado FROM cuenta c WHERE c.id = v_id_cuenta FOR SHARE;
    IF v_estado <> 'ABIERTA' THEN
        RAISE EXCEPTION 'La cuenta % no esta abierta: su detalle no se modifica', v_id_cuenta;
    END IF;

    RETURN NEW;
END;
$$;


--
-- Name: fn_pedido_mesa_ocupada(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_pedido_mesa_ocupada() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_turno  bigint;
    v_numero int;
    v_otro   int;
BEGIN
    SELECT id_turno_caja INTO v_turno FROM pedido WHERE id = NEW.id_pedido;

    -- Lock sobre la fila de la mesa, sostenido hasta el commit. Sin esto, dos
    -- meseros sentando gente en la mesa 5 al mismo tiempo pasan los dos el
    -- chequeo y la doble ocupación entra igual: el conteo de abajo no ve una
    -- fila que otra transacción todavía no commiteó. Es el mismo recurso que
    -- usa fn_pedido_numero_turno, y con la misma condición: la transacción que
    -- asocia la mesa tiene que ser corta.
    SELECT numero INTO v_numero FROM mesa WHERE id = NEW.id_mesa FOR UPDATE;

    SELECT count(*) INTO v_otro
    FROM pedido_mesa pm
    JOIN pedido p ON p.id = pm.id_pedido
    WHERE pm.id_mesa    = NEW.id_mesa
      AND pm.id_pedido <> NEW.id_pedido
      AND p.id_turno_caja = v_turno
      AND p.estado NOT IN ('CERRADO', 'ANULADO');

    -- RAISE EXCEPTION sin ERRCODE deja P0001, el único código de trigger que
    -- TryTranslateDbError traduce: devuelve el MessageText tal cual dentro de un
    -- 409. Por eso el texto va redactado para el mesero, con el número de mesa
    -- que él ve en el salón y no con el id.
    IF v_otro > 0 THEN
        RAISE EXCEPTION 'La mesa % ya está ocupada por otro pedido de este turno.', v_numero;
    END IF;

    RETURN NEW;
END;
$$;


--
-- Name: fn_pedido_numero_turno(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_pedido_numero_turno() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_turno bigint;
BEGIN
    -- El UPDATE toma un lock sobre la fila del turno abierto y lo mantiene
    -- hasta el commit de la transacción externa. Las dos consecuencias:
    --
    --   buscada    sin huecos, porque un rollback deshace también el
    --              incremento. Un salto de número en sala se lee como
    --              pedido perdido, y eso genera desconfianza.
    --   a respetar la transacción que crea el pedido tiene que ser corta.
    --              Medido: una que tarda 3 s en commitear bloquea 2.3 s al
    --              insert de otra conexión. Nada de impresión de comanda
    --              ni subida de comprobantes adentro.
    UPDATE turno_caja
       SET ultimo_numero = ultimo_numero + 1
     WHERE cerrado_en IS NULL
    RETURNING id, ultimo_numero INTO v_turno, NEW.numero_turno;

    IF v_turno IS NULL THEN
        RAISE EXCEPTION 'No hay un turno de caja abierto: abra uno antes de registrar pedidos.';
    END IF;

    NEW.id_turno_caja := v_turno;
    RETURN NEW;
END;
$$;


--
-- Name: fn_pedido_plato_facturado(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_pedido_plato_facturado() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_cuenta bigint;
BEGIN
    IF NEW.estado <> 'ANULADO' OR OLD.estado = 'ANULADO' THEN
        RETURN NEW;
    END IF;

    SELECT d.id_cuenta INTO v_cuenta FROM detalle_cuenta d WHERE d.id_pedido_plato = OLD.id;
    IF FOUND THEN
        RAISE EXCEPTION 'El plato % ya fue facturado en la cuenta %: anule la cuenta, no el plato',
            OLD.id, v_cuenta;
    END IF;

    IF NEW.anulado_en IS NULL THEN
        NEW.anulado_en := now();
    END IF;
    RETURN NEW;
END;
$$;


--
-- Name: fn_touch(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_touch() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    NEW.actualizado_en := now();
    RETURN NEW;
END;
$$;


--
-- Name: fn_turno_cierre(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_turno_cierre() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_vivos   int;
    v_cuentas int;
BEGIN
    -- Solo en la transición abierto -> cerrado. Un UPDATE sobre un turno
    -- ya cerrado no vuelve a disparar la validación.
    IF NEW.cerrado_en IS NULL OR OLD.cerrado_en IS NOT NULL THEN
        RETURN NEW;
    END IF;

    -- 1) Pedidos sin cerrar. SERVIDO cuenta como vivo: el plato ya salió,
    --    la mesa está comiendo, nadie pagó. Terminales son solo CERRADO y
    --    ANULADO.
    SELECT count(*) INTO v_vivos
    FROM pedido
    WHERE id_turno_caja = OLD.id
      AND estado NOT IN ('CERRADO', 'ANULADO');

    IF v_vivos = 1 THEN
        RAISE EXCEPTION 'No se puede cerrar el turno: queda 1 pedido sin cerrar.';
    ELSIF v_vivos > 1 THEN
        RAISE EXCEPTION 'No se puede cerrar el turno: quedan % pedidos sin cerrar.', v_vivos;
    END IF;

    -- 2) Cuentas sin cobrar. Se cuenta DISTINCT porque una cuenta reúne
    --    varias líneas de detalle, y cada comensal paga la suya: un mismo
    --    pedido puede tener varias cuentas.
    --
    --    ANULADA no bloquea. Es un cierre deliberado y documentado
    --    (ck_cuenta_anulacion exige responsable y motivo); si bloqueara,
    --    una sola anulación dejaría el turno imposible de cerrar para
    --    siempre. Solo ABIERTA bloquea.
    SELECT count(DISTINCT c.id) INTO v_cuentas
    FROM pedido p
    JOIN pedido_plato   pp ON pp.id_pedido       = p.id
    JOIN detalle_cuenta dc ON dc.id_pedido_plato = pp.id
    JOIN cuenta         c  ON c.id               = dc.id_cuenta
    WHERE p.id_turno_caja = OLD.id
      AND c.estado = 'ABIERTA';

    -- Mensaje aparte del anterior a propósito: la acción del cajero es
    -- distinta. "Sin cerrar" lo manda a cerrar pedidos; "sin cobrar", a
    -- cobrar. Un mensaje único lo mandaría al lugar equivocado.
    IF v_cuentas = 1 THEN
        RAISE EXCEPTION 'No se puede cerrar el turno: queda 1 cuenta sin cobrar.';
    ELSIF v_cuentas > 1 THEN
        RAISE EXCEPTION 'No se puede cerrar el turno: quedan % cuentas sin cobrar.', v_cuentas;
    END IF;

    RETURN NEW;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: comprobante_pago; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.comprobante_pago (
    id bigint NOT NULL,
    id_cuenta bigint NOT NULL,
    monto numeric(12,2),
    storage_key text NOT NULL,
    hash_sha256 character(64) NOT NULL,
    tipo_contenido character varying(30) NOT NULL,
    bytes integer NOT NULL,
    id_subido_por bigint NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    id_reemplaza bigint,
    motivo_reemplazo text,
    CONSTRAINT ck_comprobante_bytes CHECK ((bytes > 0)),
    CONSTRAINT ck_comprobante_monto CHECK (((monto IS NULL) OR (monto > (0)::numeric))),
    CONSTRAINT ck_comprobante_reemplazo CHECK (((id_reemplaza IS NULL) OR ((motivo_reemplazo IS NOT NULL) AND (btrim(motivo_reemplazo) <> ''::text)))),
    CONSTRAINT ck_comprobante_tipo CHECK (((tipo_contenido)::text = ANY ((ARRAY['image/webp'::character varying, 'image/jpeg'::character varying, 'image/png'::character varying])::text[])))
);


--
-- Name: comprobante_pago_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.comprobante_pago ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.comprobante_pago_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: cuenta; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cuenta (
    id bigint NOT NULL,
    comensal character varying(120),
    estado character varying(20) DEFAULT 'ABIERTA'::character varying NOT NULL,
    metodo_pago character varying(20),
    monto numeric(12,2) DEFAULT 0 NOT NULL,
    id_mesero bigint NOT NULL,
    id_cajero bigint,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    pagado_en timestamp with time zone,
    anulado_en timestamp with time zone,
    id_anulado_por bigint,
    motivo_anulacion text,
    CONSTRAINT ck_cuenta_anulacion CHECK ((((estado)::text <> 'ANULADA'::text) OR ((anulado_en IS NOT NULL) AND (id_anulado_por IS NOT NULL) AND (motivo_anulacion IS NOT NULL) AND (btrim(motivo_anulacion) <> ''::text)))),
    CONSTRAINT ck_cuenta_estado CHECK (((estado)::text = ANY ((ARRAY['ABIERTA'::character varying, 'PAGADA'::character varying, 'ANULADA'::character varying])::text[]))),
    CONSTRAINT ck_cuenta_metodo CHECK (((metodo_pago IS NULL) OR ((metodo_pago)::text = ANY ((ARRAY['EFECTIVO'::character varying, 'TARJETA'::character varying, 'TRANSFERENCIA'::character varying, 'QR'::character varying])::text[])))),
    CONSTRAINT ck_cuenta_monto CHECK ((monto >= (0)::numeric)),
    CONSTRAINT ck_cuenta_pago CHECK ((((estado)::text <> 'PAGADA'::text) OR ((metodo_pago IS NOT NULL) AND (pagado_en IS NOT NULL) AND (id_cajero IS NOT NULL))))
);


--
-- Name: cuenta_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.cuenta ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.cuenta_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: detalle_cuenta; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.detalle_cuenta (
    id bigint NOT NULL,
    id_cuenta bigint NOT NULL,
    id_pedido_plato bigint NOT NULL,
    precio_unitario numeric(10,2) NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT ck_detalle_precio CHECK ((precio_unitario >= (0)::numeric))
);


--
-- Name: detalle_cuenta_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.detalle_cuenta ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.detalle_cuenta_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: mesa; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.mesa (
    id bigint NOT NULL,
    numero integer NOT NULL,
    capacidad integer NOT NULL,
    estado character varying(20) DEFAULT 'LIBRE'::character varying NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT ck_mesa_cap CHECK ((capacidad > 0)),
    CONSTRAINT ck_mesa_estado CHECK (((estado)::text = ANY ((ARRAY['LIBRE'::character varying, 'OCUPADA'::character varying, 'RESERVADA'::character varying, 'INACTIVA'::character varying])::text[])))
);


--
-- Name: mesa_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.mesa ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.mesa_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: pedido; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.pedido (
    id bigint NOT NULL,
    comensal character varying(120),
    estado character varying(20) DEFAULT 'ABIERTO'::character varying NOT NULL,
    id_mesero bigint NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    actualizado_en timestamp with time zone DEFAULT now() NOT NULL,
    cerrado_en timestamp with time zone,
    tipo character varying(20) DEFAULT 'EN_SALON'::character varying NOT NULL,
    direccion_entrega text,
    ubicacion_compartida text,
    latitud_entrega numeric(9,6),
    longitud_entrega numeric(9,6),
    id_turno_caja bigint NOT NULL,
    numero_turno integer NOT NULL,
    CONSTRAINT ck_pedido_cierre CHECK ((((estado)::text <> 'CERRADO'::text) OR (cerrado_en IS NOT NULL))),
    CONSTRAINT ck_pedido_coordenada CHECK (((latitud_entrega IS NULL) = (longitud_entrega IS NULL))),
    CONSTRAINT ck_pedido_estado CHECK (((estado)::text = ANY ((ARRAY['ABIERTO'::character varying, 'EN_PREPARACION'::character varying, 'SERVIDO'::character varying, 'CERRADO'::character varying, 'ANULADO'::character varying])::text[]))),
    CONSTRAINT ck_pedido_latitud CHECK (((latitud_entrega IS NULL) OR ((latitud_entrega >= ('-90'::integer)::numeric) AND (latitud_entrega <= (90)::numeric)))),
    CONSTRAINT ck_pedido_longitud CHECK (((longitud_entrega IS NULL) OR ((longitud_entrega >= ('-180'::integer)::numeric) AND (longitud_entrega <= (180)::numeric)))),
    CONSTRAINT ck_pedido_tipo CHECK (((tipo)::text = ANY ((ARRAY['EN_SALON'::character varying, 'PARA_LLEVAR'::character varying, 'DELIVERY'::character varying])::text[])))
);


--
-- Name: pedido_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.pedido ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.pedido_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: pedido_mesa; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.pedido_mesa (
    id bigint NOT NULL,
    id_pedido bigint NOT NULL,
    id_mesa bigint NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: pedido_mesa_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.pedido_mesa ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.pedido_mesa_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: pedido_plato; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.pedido_plato (
    id bigint NOT NULL,
    id_pedido bigint NOT NULL,
    id_plato bigint NOT NULL,
    estado character varying(20) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    servido_en timestamp with time zone,
    anulado_en timestamp with time zone,
    id_anulado_por bigint,
    motivo_anulacion text,
    CONSTRAINT ck_pedido_plato_anulacion CHECK ((((estado)::text <> 'ANULADO'::text) OR ((anulado_en IS NOT NULL) AND (id_anulado_por IS NOT NULL) AND (motivo_anulacion IS NOT NULL) AND (btrim(motivo_anulacion) <> ''::text)))),
    CONSTRAINT ck_pedido_plato_estado CHECK (((estado)::text = ANY ((ARRAY['PENDIENTE'::character varying, 'EN_PREPARACION'::character varying, 'SERVIDO'::character varying, 'ANULADO'::character varying])::text[]))),
    CONSTRAINT ck_pedido_plato_servido CHECK ((((estado)::text <> 'SERVIDO'::text) OR (servido_en IS NOT NULL)))
);


--
-- Name: pedido_plato_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.pedido_plato ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.pedido_plato_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: plato; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.plato (
    id bigint NOT NULL,
    nombre character varying(120) NOT NULL,
    precio numeric(10,2) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    id_tipo_plato bigint NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    actualizado_en timestamp with time zone DEFAULT now() NOT NULL,
    estado character varying(20) DEFAULT 'DISPONIBLE'::character varying NOT NULL,
    habilitado_desde date DEFAULT CURRENT_DATE NOT NULL,
    habilitado_hasta date,
    CONSTRAINT ck_plato_estado CHECK (((estado)::text = ANY ((ARRAY['DISPONIBLE'::character varying, 'AGOTADO'::character varying, 'DESCONTINUADO'::character varying])::text[]))),
    CONSTRAINT ck_plato_habilitado_rango CHECK (((habilitado_hasta IS NULL) OR (habilitado_hasta >= habilitado_desde))),
    CONSTRAINT ck_plato_precio CHECK ((precio >= (0)::numeric))
);


--
-- Name: plato_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.plato ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.plato_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tipo_plato; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tipo_plato (
    id bigint NOT NULL,
    nombre character varying(80) NOT NULL,
    observacion text,
    creado_en timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: tipo_plato_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.tipo_plato ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.tipo_plato_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: turno_caja; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.turno_caja (
    id bigint NOT NULL,
    nombre character varying(40) NOT NULL,
    id_cajero bigint,
    abierto_en timestamp with time zone DEFAULT now() NOT NULL,
    cerrado_en timestamp with time zone,
    ultimo_numero integer DEFAULT 0 NOT NULL,
    CONSTRAINT ck_turno_caja_cierre CHECK (((cerrado_en IS NULL) OR (cerrado_en >= abierto_en))),
    CONSTRAINT ck_turno_caja_nombre CHECK ((btrim((nombre)::text) <> ''::text))
);


--
-- Name: turno_caja_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.turno_caja ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.turno_caja_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: usuario; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.usuario (
    id bigint NOT NULL,
    nombre character varying(120) NOT NULL,
    rol character varying(20) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    actualizado_en timestamp with time zone DEFAULT now() NOT NULL,
    nombre_usuario character varying(60) NOT NULL,
    password_hash character varying(200) NOT NULL,
    CONSTRAINT ck_usuario_rol CHECK (((rol)::text = ANY ((ARRAY['MESERO'::character varying, 'CAJERO'::character varying, 'COCINERO'::character varying, 'ADMIN'::character varying])::text[])))
);


--
-- Name: usuario_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.usuario ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.usuario_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: v_comprobante_duplicado; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.v_comprobante_duplicado AS
 SELECT id,
    id_cuenta,
    hash_sha256,
    monto,
    creado_en,
    id_subido_por
   FROM public.comprobante_pago cp
  WHERE (hash_sha256 IN ( SELECT comprobante_pago.hash_sha256
           FROM public.comprobante_pago
          GROUP BY comprobante_pago.hash_sha256
         HAVING (count(DISTINCT comprobante_pago.id_cuenta) > 1)));


--
-- Name: v_comprobante_vigente; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.v_comprobante_vigente AS
 SELECT id,
    id_cuenta,
    monto,
    storage_key,
    hash_sha256,
    id_subido_por,
    creado_en
   FROM public.comprobante_pago cp
  WHERE (NOT (EXISTS ( SELECT 1
           FROM public.comprobante_pago r
          WHERE (r.id_reemplaza = cp.id))));


--
-- Name: v_cuenta_descuadrada; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.v_cuenta_descuadrada AS
SELECT
    NULL::bigint AS id,
    NULL::character varying(20) AS estado,
    NULL::numeric(12,2) AS monto,
    NULL::numeric AS suma_detalle,
    NULL::timestamp with time zone AS pagado_en;


--
-- Name: v_cuenta_qr_evidencia_incompleta; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.v_cuenta_qr_evidencia_incompleta AS
SELECT
    NULL::bigint AS id,
    NULL::character varying(120) AS comensal,
    NULL::character varying(20) AS estado,
    NULL::numeric(12,2) AS monto,
    NULL::timestamp with time zone AS pagado_en,
    NULL::bigint AS id_mesero,
    NULL::bigint AS comprobantes,
    NULL::bigint AS sin_monto,
    NULL::numeric AS monto_respaldado;


--
-- Name: v_pedido_plato_sin_cobrar; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.v_pedido_plato_sin_cobrar AS
 SELECT pp.id AS id_pedido_plato,
    pp.id_pedido,
    pl.nombre AS plato,
    pl.precio,
    pp.estado
   FROM ((public.pedido_plato pp
     JOIN public.plato pl ON ((pl.id = pp.id_plato)))
     LEFT JOIN public.detalle_cuenta dc ON ((dc.id_pedido_plato = pp.id)))
  WHERE ((dc.id IS NULL) AND ((pp.estado)::text <> 'ANULADO'::text));


--
-- Name: comprobante_pago comprobante_pago_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.comprobante_pago
    ADD CONSTRAINT comprobante_pago_pkey PRIMARY KEY (id);


--
-- Name: cuenta cuenta_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cuenta
    ADD CONSTRAINT cuenta_pkey PRIMARY KEY (id);


--
-- Name: detalle_cuenta detalle_cuenta_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_cuenta
    ADD CONSTRAINT detalle_cuenta_pkey PRIMARY KEY (id);


--
-- Name: mesa mesa_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.mesa
    ADD CONSTRAINT mesa_pkey PRIMARY KEY (id);


--
-- Name: pedido_mesa pedido_mesa_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pedido_mesa
    ADD CONSTRAINT pedido_mesa_pkey PRIMARY KEY (id);


--
-- Name: pedido pedido_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pedido
    ADD CONSTRAINT pedido_pkey PRIMARY KEY (id);


--
-- Name: pedido_plato pedido_plato_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pedido_plato
    ADD CONSTRAINT pedido_plato_pkey PRIMARY KEY (id);


--
-- Name: plato plato_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.plato
    ADD CONSTRAINT plato_pkey PRIMARY KEY (id);


--
-- Name: tipo_plato tipo_plato_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipo_plato
    ADD CONSTRAINT tipo_plato_pkey PRIMARY KEY (id);


--
-- Name: turno_caja turno_caja_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.turno_caja
    ADD CONSTRAINT turno_caja_pkey PRIMARY KEY (id);


--
-- Name: comprobante_pago uk_comprobante_cuenta_hash; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.comprobante_pago
    ADD CONSTRAINT uk_comprobante_cuenta_hash UNIQUE (id_cuenta, hash_sha256);


--
-- Name: comprobante_pago uk_comprobante_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.comprobante_pago
    ADD CONSTRAINT uk_comprobante_key UNIQUE (storage_key);


--
-- Name: comprobante_pago uk_comprobante_reemplaza; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.comprobante_pago
    ADD CONSTRAINT uk_comprobante_reemplaza UNIQUE (id_reemplaza);


--
-- Name: detalle_cuenta uk_detalle_pedido_plato; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_cuenta
    ADD CONSTRAINT uk_detalle_pedido_plato UNIQUE (id_pedido_plato);


--
-- Name: mesa uk_mesa_numero; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.mesa
    ADD CONSTRAINT uk_mesa_numero UNIQUE (numero);


--
-- Name: pedido_mesa uk_pedido_mesa; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pedido_mesa
    ADD CONSTRAINT uk_pedido_mesa UNIQUE (id_pedido, id_mesa);


--
-- Name: pedido uk_pedido_numero_turno; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pedido
    ADD CONSTRAINT uk_pedido_numero_turno UNIQUE (id_turno_caja, numero_turno);


--
-- Name: tipo_plato uk_tipo_plato_nombre; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipo_plato
    ADD CONSTRAINT uk_tipo_plato_nombre UNIQUE (nombre);


--
-- Name: usuario uk_usuario_nombre_usuario; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario
    ADD CONSTRAINT uk_usuario_nombre_usuario UNIQUE (nombre_usuario);


--
-- Name: usuario usuario_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario
    ADD CONSTRAINT usuario_pkey PRIMARY KEY (id);


--
-- Name: ix_comprobante_cuenta; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_comprobante_cuenta ON public.comprobante_pago USING btree (id_cuenta, creado_en DESC);


--
-- Name: ix_comprobante_hash; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_comprobante_hash ON public.comprobante_pago USING btree (hash_sha256);


--
-- Name: ix_cuenta_cajero; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_cuenta_cajero ON public.cuenta USING btree (id_cajero);


--
-- Name: ix_cuenta_mesero; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_cuenta_mesero ON public.cuenta USING btree (id_mesero);


--
-- Name: ix_cuenta_pago; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_cuenta_pago ON public.cuenta USING btree (pagado_en);


--
-- Name: ix_detalle_cuenta_cuenta; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_detalle_cuenta_cuenta ON public.detalle_cuenta USING btree (id_cuenta);


--
-- Name: ix_pedido_fecha; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_pedido_fecha ON public.pedido USING btree (creado_en);


--
-- Name: ix_pedido_mesa_mesa; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_pedido_mesa_mesa ON public.pedido_mesa USING btree (id_mesa);


--
-- Name: ix_pedido_mesero; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_pedido_mesero ON public.pedido USING btree (id_mesero);


--
-- Name: ix_pedido_plato_pedido; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_pedido_plato_pedido ON public.pedido_plato USING btree (id_pedido);


--
-- Name: ix_pedido_plato_plato; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_pedido_plato_plato ON public.pedido_plato USING btree (id_plato);


--
-- Name: ix_plato_tipo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_plato_tipo ON public.plato USING btree (id_tipo_plato);


--
-- Name: ix_turno_caja_abierto_en; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_turno_caja_abierto_en ON public.turno_caja USING btree (abierto_en);


--
-- Name: uk_pedido_comensal_activo; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uk_pedido_comensal_activo ON public.pedido USING btree (id_turno_caja, comensal) WHERE ((estado)::text = ANY ((ARRAY['ABIERTO'::character varying, 'EN_PREPARACION'::character varying])::text[]));


--
-- Name: uk_turno_caja_abierto; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uk_turno_caja_abierto ON public.turno_caja USING btree ((true)) WHERE (cerrado_en IS NULL);


--
-- Name: v_cuenta_descuadrada _RETURN; Type: RULE; Schema: public; Owner: -
--

CREATE OR REPLACE VIEW public.v_cuenta_descuadrada AS
 SELECT c.id,
    c.estado,
    c.monto,
    COALESCE(sum(d.precio_unitario), (0)::numeric) AS suma_detalle,
    c.pagado_en
   FROM (public.cuenta c
     LEFT JOIN public.detalle_cuenta d ON ((d.id_cuenta = c.id)))
  WHERE ((c.estado)::text <> 'ANULADA'::text)
  GROUP BY c.id
 HAVING (c.monto IS DISTINCT FROM COALESCE(sum(d.precio_unitario), (0)::numeric));


--
-- Name: v_cuenta_qr_evidencia_incompleta _RETURN; Type: RULE; Schema: public; Owner: -
--

CREATE OR REPLACE VIEW public.v_cuenta_qr_evidencia_incompleta AS
 SELECT c.id,
    c.comensal,
    c.estado,
    c.monto,
    c.pagado_en,
    c.id_mesero,
    count(v.id) AS comprobantes,
    count(v.id) FILTER (WHERE (v.monto IS NULL)) AS sin_monto,
    COALESCE(sum(v.monto), (0)::numeric) AS monto_respaldado
   FROM (public.cuenta c
     LEFT JOIN public.v_comprobante_vigente v ON ((v.id_cuenta = c.id)))
  WHERE (((c.estado)::text <> 'ANULADA'::text) AND ((c.metodo_pago)::text = 'QR'::text))
  GROUP BY c.id
 HAVING ((count(v.id) = 0) OR ((count(v.id) FILTER (WHERE (v.monto IS NULL)) = 0) AND (c.monto IS DISTINCT FROM COALESCE(sum(v.monto), (0)::numeric))));


--
-- Name: comprobante_pago tg_comprobante_inmutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER tg_comprobante_inmutable BEFORE INSERT OR DELETE OR UPDATE ON public.comprobante_pago FOR EACH ROW EXECUTE FUNCTION public.fn_comprobante_inmutable();


--
-- Name: cuenta tg_cuenta_inmutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER tg_cuenta_inmutable BEFORE DELETE OR UPDATE ON public.cuenta FOR EACH ROW EXECUTE FUNCTION public.fn_cuenta_inmutable();


--
-- Name: detalle_cuenta tg_detalle_inmutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER tg_detalle_inmutable BEFORE INSERT OR DELETE OR UPDATE ON public.detalle_cuenta FOR EACH ROW EXECUTE FUNCTION public.fn_detalle_inmutable();


--
-- Name: pedido_mesa tg_pedido_mesa_ocupada; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER tg_pedido_mesa_ocupada BEFORE INSERT OR UPDATE ON public.pedido_mesa FOR EACH ROW EXECUTE FUNCTION public.fn_pedido_mesa_ocupada();


--
-- Name: pedido tg_pedido_numero_turno; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER tg_pedido_numero_turno BEFORE INSERT ON public.pedido FOR EACH ROW EXECUTE FUNCTION public.fn_pedido_numero_turno();


--
-- Name: pedido_plato tg_pedido_plato_facturado; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER tg_pedido_plato_facturado BEFORE UPDATE ON public.pedido_plato FOR EACH ROW EXECUTE FUNCTION public.fn_pedido_plato_facturado();


--
-- Name: pedido tg_touch_pedido; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER tg_touch_pedido BEFORE UPDATE ON public.pedido FOR EACH ROW EXECUTE FUNCTION public.fn_touch();


--
-- Name: plato tg_touch_plato; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER tg_touch_plato BEFORE UPDATE ON public.plato FOR EACH ROW EXECUTE FUNCTION public.fn_touch();


--
-- Name: usuario tg_touch_usuario; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER tg_touch_usuario BEFORE UPDATE ON public.usuario FOR EACH ROW EXECUTE FUNCTION public.fn_touch();


--
-- Name: turno_caja tg_turno_cierre; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER tg_turno_cierre BEFORE UPDATE ON public.turno_caja FOR EACH ROW EXECUTE FUNCTION public.fn_turno_cierre();


--
-- Name: comprobante_pago fk_comprobante_cuenta; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.comprobante_pago
    ADD CONSTRAINT fk_comprobante_cuenta FOREIGN KEY (id_cuenta) REFERENCES public.cuenta(id) ON DELETE RESTRICT;


--
-- Name: comprobante_pago fk_comprobante_reemplaza; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.comprobante_pago
    ADD CONSTRAINT fk_comprobante_reemplaza FOREIGN KEY (id_reemplaza) REFERENCES public.comprobante_pago(id) ON DELETE RESTRICT;


--
-- Name: comprobante_pago fk_comprobante_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.comprobante_pago
    ADD CONSTRAINT fk_comprobante_usuario FOREIGN KEY (id_subido_por) REFERENCES public.usuario(id) ON DELETE RESTRICT;


--
-- Name: cuenta fk_cuenta_anulador; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cuenta
    ADD CONSTRAINT fk_cuenta_anulador FOREIGN KEY (id_anulado_por) REFERENCES public.usuario(id) ON DELETE RESTRICT;


--
-- Name: cuenta fk_cuenta_cajero; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cuenta
    ADD CONSTRAINT fk_cuenta_cajero FOREIGN KEY (id_cajero) REFERENCES public.usuario(id) ON DELETE RESTRICT;


--
-- Name: cuenta fk_cuenta_mesero; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cuenta
    ADD CONSTRAINT fk_cuenta_mesero FOREIGN KEY (id_mesero) REFERENCES public.usuario(id) ON DELETE RESTRICT;


--
-- Name: detalle_cuenta fk_detalle_cuenta_cuenta; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_cuenta
    ADD CONSTRAINT fk_detalle_cuenta_cuenta FOREIGN KEY (id_cuenta) REFERENCES public.cuenta(id) ON DELETE RESTRICT;


--
-- Name: detalle_cuenta fk_detalle_cuenta_pedido_plato; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_cuenta
    ADD CONSTRAINT fk_detalle_cuenta_pedido_plato FOREIGN KEY (id_pedido_plato) REFERENCES public.pedido_plato(id) ON DELETE RESTRICT;


--
-- Name: pedido_mesa fk_pedido_mesa_mesa; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pedido_mesa
    ADD CONSTRAINT fk_pedido_mesa_mesa FOREIGN KEY (id_mesa) REFERENCES public.mesa(id) ON DELETE RESTRICT;


--
-- Name: pedido_mesa fk_pedido_mesa_pedido; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pedido_mesa
    ADD CONSTRAINT fk_pedido_mesa_pedido FOREIGN KEY (id_pedido) REFERENCES public.pedido(id) ON DELETE RESTRICT;


--
-- Name: pedido fk_pedido_mesero; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pedido
    ADD CONSTRAINT fk_pedido_mesero FOREIGN KEY (id_mesero) REFERENCES public.usuario(id) ON DELETE RESTRICT;


--
-- Name: pedido_plato fk_pedido_plato_anulador; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pedido_plato
    ADD CONSTRAINT fk_pedido_plato_anulador FOREIGN KEY (id_anulado_por) REFERENCES public.usuario(id) ON DELETE RESTRICT;


--
-- Name: pedido_plato fk_pedido_plato_pedido; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pedido_plato
    ADD CONSTRAINT fk_pedido_plato_pedido FOREIGN KEY (id_pedido) REFERENCES public.pedido(id) ON DELETE RESTRICT;


--
-- Name: pedido_plato fk_pedido_plato_plato; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pedido_plato
    ADD CONSTRAINT fk_pedido_plato_plato FOREIGN KEY (id_plato) REFERENCES public.plato(id) ON DELETE RESTRICT;


--
-- Name: pedido fk_pedido_turno_caja; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pedido
    ADD CONSTRAINT fk_pedido_turno_caja FOREIGN KEY (id_turno_caja) REFERENCES public.turno_caja(id) ON DELETE RESTRICT;


--
-- Name: plato fk_plato_tipo; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.plato
    ADD CONSTRAINT fk_plato_tipo FOREIGN KEY (id_tipo_plato) REFERENCES public.tipo_plato(id) ON DELETE RESTRICT;


--
-- Name: turno_caja fk_turno_caja_cajero; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.turno_caja
    ADD CONSTRAINT fk_turno_caja_cajero FOREIGN KEY (id_cajero) REFERENCES public.usuario(id) ON DELETE RESTRICT;


--
-- PostgreSQL database dump complete
--

\unrestrict q7QXGFjmQg3ZF9PgUUSabYPzVKemq4RcDnoyT1dv5pzmiQzcC9E4z3mpU5h1Q1E

COMMIT;


-- #####################################################################
-- BLOQUE 2 — ROL DE APLICACIÓN (opcional)
-- Requiere superusuario o CREATEROLE.
-- Sin DELETE: los borrados se hacen anulando, no eliminando filas.
-- #####################################################################

BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_restaurante') THEN
        CREATE ROLE app_restaurante LOGIN;
    END IF;
END $$;

GRANT USAGE ON SCHEMA public TO app_restaurante;

GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO app_restaurante;
REVOKE DELETE, TRUNCATE ON ALL TABLES IN SCHEMA public FROM app_restaurante;

-- No se otorgan privilegios sobre secuencias: GENERATED ALWAYS AS IDENTITY
-- no los necesita, el permiso se verifica contra el INSERT de la tabla.

ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE ON TABLES TO app_restaurante;

COMMIT;

-- Asignar la contraseña aparte, fuera de este archivo:
--   ALTER ROLE app_restaurante WITH PASSWORD '<clave>';
