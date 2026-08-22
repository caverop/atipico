using System.Globalization;
using System.Text.RegularExpressions;

namespace Atipico.Application.Common
{
    /// <summary>
    /// Extrae un punto GPS de lo que el comensal comparte por WhatsApp y el mesero pega.
    ///
    /// Vive en Application y no en la pantalla a proposito: es logica pura, sin base ni HTTP,
    /// y el agente de WhatsApp (docs/direccion-entrega.md §7) la va a necesitar. Una regla de
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

            foreach (Match match in ParDeCoordenadas.Matches(texto))
            {
                // InvariantCulture: el separador decimal siempre es punto, venga de donde
                // venga el texto. Con la cultura del servidor, "-17.783" se leeria como
                // -17783 en una cultura que use la coma.
                if (!decimal.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                    !decimal.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
                    continue;

                if (!EnRango(lat, lng))
                    continue;

                latitud = lat;
                longitud = lng;
                return true;
            }

            return false;
        }

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
    }
}
