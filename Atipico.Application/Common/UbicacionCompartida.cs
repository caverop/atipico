using System.Globalization;
using System.Text.RegularExpressions;

namespace Atipico.Application.Common
{
    /// <summary>
    /// Extrae un punto GPS de lo que el comensal comparte por WhatsApp y el mesero pega.
    ///
    /// Vive en Application y no en la pantalla a proposito: es logica pura, sin base ni HTTP,
    /// y el agente de WhatsApp (specs/direccion-entrega.md §7) la va a necesitar. Una regla de
    /// negocio metida en un .razor solo puede terminar reimplementada distinto.
    /// </summary>
    public static class UbicacionCompartida
    {
        /// <summary>
        /// Bolivia, en numeros redondos y con margen. Sirve para detectar coordenadas
        /// invertidas — el error clasico al teclearlas a mano —, no para rechazar nada:
        /// una caja mal calibrada bloqueando pedidos reales seria peor que el error que evita.
        /// </summary>
        private const decimal LatitudMinima = -23.5m;
        private const decimal LatitudMaxima = -9.0m;
        private const decimal LongitudMinima = -70.0m;
        private const decimal LongitudMaxima = -57.0m;

        // No se parsean URLs: se busca el primer par de numeros con decimales que aparezca en
        // el texto. Eso cubre "?q=lat,lng", "/@lat,lng,17z" y el "lat,lng" pegado a mano sin
        // conocer ningun formato en particular, y sin romperse cuando Google cambie el suyo.
        // El ",17z" del nivel de zoom no interfiere: no tiene parte decimal.
        private static readonly Regex ParDeCoordenadas = new(
            @"(-?\d{1,3}\.\d+)\s*,\s*(-?\d{1,3}\.\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Una URL de "place" de Google trae DOS pares y no son el mismo punto:
        //
        //   .../@-17.7637728,-63.2002991,18.94z/data=...!3d-17.763701!4d-63.19992
        //        ^^^^^^^^^^^^^^^^^^^^^^^^ el encuadre de la camara    ^^^^^^^^^^^ el pin
        //
        // El de la camara es el que aparece primero, asi que buscar "el primer par" agarra el
        // equivocado — en el ejemplo de arriba, a unos 40 metros del pin real. Este patron se
        // prueba ANTES que el generico. Los numeros van sin coma en el medio, por eso el
        // patron de arriba no los ve.
        private static readonly Regex PinDeGoogleMaps = new(
            @"!3d(-?\d{1,3}\.\d+)!4d(-?\d{1,3}\.\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Intenta sacar el punto del texto pegado. Devuelve false cuando no hay ninguno:
        /// no es un error, es el caso de un enlace corto maps.app.goo.gl, que no trae las
        /// coordenadas. El texto se guarda igual y una persona lo abre.
        /// </summary>
        public static bool TryExtraer(string? texto, out decimal latitud, out decimal longitud)
        {
            latitud = 0;
            longitud = 0;

            if (string.IsNullOrWhiteSpace(texto))
                return false;

            // El pin gana sobre el encuadre cuando los dos estan presentes.
            var pin = PinDeGoogleMaps.Match(texto);
            if (pin.Success && TryLeerPar(pin, out latitud, out longitud))
                return true;

            foreach (Match match in ParDeCoordenadas.Matches(texto))
            {
                if (TryLeerPar(match, out latitud, out longitud))
                    return true;
            }

            return false;
        }

        // ---- Enlaces cortos ---------------------------------------------------------
        // Ver specs/enlace-corto-ubicacion.md. Aca vive solo la DECISION (¿esta URL vale la
        // pena resolverla?), que es logica pura y se puede probar. La peticion HTTP la hace
        // Atipico.Web/Services/ResolvedorEnlaceUbicacion.cs, porque Atipico.Web no tiene
        // proyecto de pruebas y conviene que ahi quede lo menos posible.

        /// <summary>
        /// Los unicos anfitriones cuyo redirect se sigue. Es un requisito funcional antes que
        /// nada: seguir una URL que no sea de Google Maps no puede dar coordenadas, asi que
        /// intentarlo es trabajo perdido y una espera para el cajero.
        ///
        /// Y cierra una puerta: la URL no la escribe el cajero, la escribe el comensal y el
        /// cajero la reenvia. Sin la lista, quien manda el mensaje elige a que direccion hace
        /// una peticion el servidor.
        /// </summary>
        private static readonly HashSet<string> AnfitrionesResolubles = new(StringComparer.OrdinalIgnoreCase)
        {
            "maps.app.goo.gl",
            "goo.gl",
            "maps.google.com",
            "www.google.com",
            "google.com",
        };

        private static readonly Regex PrimeraUrl = new(
            @"https://[^\s<>""']+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// True si el texto trae una URL que vale la pena resolver. Se llama solo cuando
        /// <see cref="TryExtraer"/> ya fallo: un texto que ya tiene coordenadas no necesita
        /// ninguna peticion.
        /// </summary>
        public static bool TryObtenerEnlaceResoluble(string? texto, out Uri? enlace)
        {
            enlace = null;

            if (string.IsNullOrWhiteSpace(texto))
                return false;

            var match = PrimeraUrl.Match(texto);
            if (!match.Success)
                return false;

            if (!Uri.TryCreate(match.Value, UriKind.Absolute, out var uri) || !EsResoluble(uri))
                return false;

            enlace = uri;
            return true;
        }

        /// <summary>
        /// Se aplica a la URL pegada y tambien a CADA salto del redirect. Validar solo la
        /// primera dejaria la puerta abierta en el segundo paso, porque un redirect puede
        /// apuntar a cualquier parte.
        /// </summary>
        public static bool EsResoluble(Uri? enlace) =>
            enlace is not null
            && enlace.Scheme == Uri.UriSchemeHttps
            && AnfitrionesResolubles.Contains(enlace.Host);

        /// <summary>
        /// Convierte los dos grupos de una coincidencia en un punto valido, o devuelve false
        /// para que quien llama siga buscando.
        /// </summary>
        private static bool TryLeerPar(Match match, out decimal latitud, out decimal longitud)
        {
            latitud = 0;
            longitud = 0;

            // InvariantCulture: el separador decimal siempre es punto, venga de donde venga el
            // texto. Con la cultura del servidor, "-17.783" se leeria como -17783 en una
            // cultura que use la coma.
            if (!decimal.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                !decimal.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
                return false;

            if (!EnRango(lat, lng))
                return false;

            // Se redondea ACA y no al guardar. La columna es numeric(9,6) y Postgres
            // redondearia igual, pero entonces la pantalla mostraria los quince decimales que
            // pego el mesero y recien al recargar aparecerian seis: se lee como que la
            // aplicacion perdio datos. Redondeando al leer, lo que se ve es lo que se guarda
            // desde el primer momento.
            latitud = Redondear(lat);
            longitud = Redondear(lng);
            return true;
        }

        /// <summary>
        /// Decimales que admite numeric(9,6). Un grado de latitud son unos 111 km, asi que
        /// seis decimales son unos 11 cm — mucho mas fino que cualquier GPS de telefono, que
        /// en la calle acierta dentro de 3 a 10 metros. Los decimales que vienen despues no
        /// son precision: son el ruido de representar el punto en punto flotante.
        ///
        /// El texto original, con todos sus decimales, se conserva igual en
        /// pedido.ubicacion_compartida. No se pierde nada.
        /// </summary>
        private const int Decimales = 6;

        // AwayFromZero y no el ToEven que usa .NET por defecto: es como redondea Postgres al
        // guardar en numeric. Si difirieran, un valor podria cambiar al pasar por la base y
        // romper la comparacion por igualdad de EsReferenciaPorDefecto.
        private static decimal Redondear(decimal valor) =>
            decimal.Round(valor, Decimales, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Rangos del sistema de coordenadas, los mismos que ck_pedido_latitud y
        /// ck_pedido_longitud. Se validan aca ademas de en la base para no mandar un INSERT
        /// que se sabe que va a fallar.
        /// </summary>
        public static bool EnRango(decimal latitud, decimal longitud) =>
            latitud is >= -90 and <= 90 && longitud is >= -180 and <= 180;

        /// <summary>
        /// True cuando el punto cae fuera de Bolivia. Casi siempre significa que lat y lng
        /// estan al reves; tambien atrapa el (0,0) de un GPS sin señal, que pasa todos los
        /// rangos y apunta al golfo de Guinea.
        /// </summary>
        public static bool FueraDeBolivia(decimal latitud, decimal longitud) =>
            latitud < LatitudMinima || latitud > LatitudMaxima
            || longitud < LongitudMinima || longitud > LongitudMaxima;

        /// <summary>
        /// Enlace para abrir el punto en la app de mapas del telefono. Se arma a mano en vez
        /// de guardar el original porque el original puede caducar: un enlace de "ubicacion
        /// en tiempo real" muere a las horas, unas coordenadas no.
        /// </summary>
        public static string EnlaceMapa(decimal latitud, decimal longitud) =>
            FormattableString.Invariant($"https://www.google.com/maps?q={latitud},{longitud}");

        /// <summary>
        /// Punto de referencia del local, con el que arranca un pedido nuevo y sobre el que
        /// se centra el mapa cuando el pedido todavia no tiene ubicacion propia.
        ///
        /// Van con SEIS decimales y no con los quince del original: la columna es
        /// numeric(9,6) y Postgres redondearia igual. Guardarlos ya redondeados evita que el
        /// valor en codigo y el valor en la base no coincidan, que es lo que hace fallar una
        /// comparacion por igualdad como la de EsReferenciaPorDefecto.
        /// </summary>
        public const decimal LatitudPorDefecto = -17.763701m;

        public const decimal LongitudPorDefecto = -63.199920m;

        /// <summary>
        /// True si el punto sigue siendo el de referencia, o sea que nadie lo movio. Se usa
        /// para que el aviso de "delivery sin direccion" no se apague solo por el valor por
        /// defecto: un pedido que apunta al local no tiene a donde ir.
        /// </summary>
        public static bool EsReferenciaPorDefecto(decimal? latitud, decimal? longitud) =>
            latitud == LatitudPorDefecto && longitud == LongitudPorDefecto;

        /// <summary>
        /// El par como se pega y se lee: "lat, lng". Invariante, por lo mismo que el resto.
        /// </summary>
        public static string Formatear(decimal latitud, decimal longitud) =>
            FormattableString.Invariant($"{latitud}, {longitud}");

        /// <summary>
        /// Medio lado de la caja del mapa embebido, en grados. Un grado de latitud son unos
        /// 111 km en todo el planeta, asi que 0.0015 da unos 330 m de lado: se ve la cuadra y
        /// las de al lado. En longitud son 111 km x cos(latitud), pero en Bolivia (-17.8°)
        /// eso es apenas un 4% menos, asi que la caja se ve cuadrada sin corregir nada.
        /// </summary>
        private const decimal MargenMapa = 0.0015m;

        /// <summary>
        /// URL del mapa embebido de OpenStreetMap centrado en el punto.
        ///
        /// Ojo con el orden: <c>bbox</c> va LONGITUD primero y <c>marker</c> va LATITUD
        /// primero, en la misma URL. Invertirlos no falla: dibuja un mapa impecable de otro
        /// lugar.
        /// </summary>
        public static string EmbedMapa(decimal latitud, decimal longitud)
        {
            // Nombrados en vez de calculados dentro de la interpolacion: es lo que hace
            // visible que el bbox va lng,lat,lng,lat y no al reves. (Concatenar cadenas
            // interpoladas tampoco serviria: FormattableString.Invariant necesita una sola.)
            var minLng = longitud - MargenMapa;
            var minLat = latitud - MargenMapa;
            var maxLng = longitud + MargenMapa;
            var maxLat = latitud + MargenMapa;

            return FormattableString.Invariant(
                $"https://www.openstreetmap.org/export/embed.html?bbox={minLng},{minLat},{maxLng},{maxLat}&layer=mapnik&marker={latitud},{longitud}");
        }
    }
}
