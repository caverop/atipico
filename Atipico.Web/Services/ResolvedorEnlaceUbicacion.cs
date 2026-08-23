using Atipico.Application.Common;

namespace Atipico.Web.Services
{
    /// <summary>
    /// Resultado de intentar resolver un enlace. Distingue "no era un enlace resoluble" de
    /// "lo era y la consulta fallo": para el usuario son situaciones distintas y necesitan
    /// mensajes distintos. Sin esa distincion, un contenedor sin salida a internet se ve
    /// igual que un texto sin coordenadas, y no hay forma de darse cuenta.
    /// </summary>
    /// <param name="SeIntento">Falso cuando el texto no traia una URL de las que se siguen.</param>
    public sealed record ResolucionUbicacion(bool SeIntento, decimal? Latitud, decimal? Longitud)
    {
        public bool Resuelto => Latitud is not null && Longitud is not null;

        public static readonly ResolucionUbicacion NoAplica = new(false, null, null);

        public static readonly ResolucionUbicacion Fallo = new(true, null, null);

        public static ResolucionUbicacion Exito(decimal latitud, decimal longitud) =>
            new(true, latitud, longitud);
    }

    /// <summary>
    /// Sigue el redirect de un enlace corto de Google Maps hasta encontrar las coordenadas.
    /// Ver docs/enlace-corto-ubicacion.md.
    /// </summary>
    public interface IResolvedorEnlaceUbicacion
    {
        Task<ResolucionUbicacion> ResolverAsync(string? texto, CancellationToken ct = default);
    }

    public class ResolvedorEnlaceUbicacion : IResolvedorEnlaceUbicacion
    {
        /// <summary>
        /// Nombre del HttpClient configurado en Program.cs. No es el de la API: este no lleva
        /// el token del usuario ni sigue redirects solo.
        /// </summary>
        public const string ClienteHttp = "EnlaceUbicacion";

        // Tres alcanzan de sobra: el acortador hace uno, y Google a veces mete uno mas. Un
        // tope tambien corta una cadena de redirects que se muerda la cola.
        private const int SaltosMaximos = 3;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ResolvedorEnlaceUbicacion> _logger;

        public ResolvedorEnlaceUbicacion(
            IHttpClientFactory httpClientFactory,
            ILogger<ResolvedorEnlaceUbicacion> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<ResolucionUbicacion> ResolverAsync(string? texto, CancellationToken ct = default)
        {
            if (!UbicacionCompartida.TryObtenerEnlaceResoluble(texto, out var enlace))
                return ResolucionUbicacion.NoAplica;

            var cliente = _httpClientFactory.CreateClient(ClienteHttp);

            try
            {
                for (var salto = 0; salto < SaltosMaximos && enlace is not null; salto++)
                {
                    // ResponseHeadersRead: solo interesa la cabecera Location. No hay motivo
                    // para descargar el cuerpo de una respuesta que es un redirect.
                    using var solicitud = new HttpRequestMessage(HttpMethod.Get, enlace);
                    using var respuesta = await cliente.SendAsync(
                        solicitud, HttpCompletionOption.ResponseHeadersRead, ct);

                    var destino = respuesta.Headers.Location;
                    if (destino is null)
                    {
                        _logger.LogWarning(
                            "El enlace de ubicación respondió {Estado} sin cabecera Location desde {Anfitrion}.",
                            (int)respuesta.StatusCode, enlace.Host);
                        return ResolucionUbicacion.Fallo;
                    }

                    // Location puede venir relativa.
                    if (!destino.IsAbsoluteUri)
                        destino = new Uri(enlace, destino);

                    // Cada salto se valida igual que el primero: un redirect puede apuntar a
                    // cualquier parte, y validar solo la URL pegada dejaria la puerta abierta
                    // aca.
                    if (!UbicacionCompartida.EsResoluble(destino))
                    {
                        _logger.LogWarning(
                            "El enlace de ubicación redirigió a {Anfitrion}, que no está en la lista.",
                            destino.Host);
                        return ResolucionUbicacion.Fallo;
                    }

                    if (UbicacionCompartida.TryExtraer(destino.ToString(), out var lat, out var lng))
                        return ResolucionUbicacion.Exito(lat, lng);

                    enlace = destino;
                }

                _logger.LogWarning("El enlace de ubicación no expuso coordenadas en {Saltos} saltos.", SaltosMaximos);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
            {
                // Warning y no Information: era un enlace de Google, o sea que se esperaba que
                // resolviera. Si esto aparece seguido, lo primero a mirar es si el contenedor
                // tiene salida a internet — nunca la necesito antes de esta funcionalidad.
                _logger.LogWarning(ex, "No se pudo consultar el enlace de ubicación.");
            }

            return ResolucionUbicacion.Fallo;
        }
    }
}
