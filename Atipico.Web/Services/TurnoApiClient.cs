using Atipico.Application.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atipico.Web.Services
{
    public class TurnoApiClient : ITurnoApiClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _http;

        public TurnoApiClient(IHttpClientFactory httpClientFactory)
        {
            _http = httpClientFactory.CreateClient("AtipicoApi");
        }

        public async Task<GrillaPedidosDto> GetGrillaAbiertaAsync() =>
            await LeerGrillaAsync("pedidos/turno-abierto");

        public async Task<GrillaPedidosDto> GetGrillaAsync(long idTurno) =>
            await LeerGrillaAsync($"pedidos/turno/{idTurno}");

        public async Task<List<TurnoDto>> GetHistoricoAsync()
        {
            var response = await _http.GetAsync("turnos");
            if (!response.IsSuccessStatusCode)
                throw new ApiException(await LeerMensajeErrorAsync(response));

            return await response.Content.ReadFromJsonAsync<List<TurnoDto>>(JsonOptions) ?? [];
        }

        public async Task<TurnoDto?> AbrirAsync(string nombre)
        {
            var response = await _http.PostAsJsonAsync("turnos", new AbrirTurnoRequest { Nombre = nombre }, JsonOptions);
            if (!response.IsSuccessStatusCode)
                throw await ErrorDeCierreAsync(response);

            return await response.Content.ReadFromJsonAsync<TurnoDto>(JsonOptions);
        }

        public async Task CerrarAsync()
        {
            var response = await _http.PostAsync("turnos/cerrar", content: null);
            if (!response.IsSuccessStatusCode)
                throw await ErrorDeCierreAsync(response);
        }

        private async Task<GrillaPedidosDto> LeerGrillaAsync(string ruta)
        {
            var response = await _http.GetAsync(ruta);
            if (!response.IsSuccessStatusCode)
                throw new ApiException(await LeerMensajeErrorAsync(response));

            // 204 no lo devuelve ninguna de las dos rutas de grilla (sin turno abierto viene
            // un 200 con Turno en null), pero un cuerpo vacio no debe reventar la pantalla.
            if (response.StatusCode == HttpStatusCode.NoContent)
                return new GrillaPedidosDto(null, []);

            return await response.Content.ReadFromJsonAsync<GrillaPedidosDto>(JsonOptions)
                   ?? new GrillaPedidosDto(null, []);
        }

        /// <summary>
        /// El 409 de cierre bloqueado trae, ademas del mensaje, la lista de pedidos que lo
        /// impiden. Si viene, se conserva: mandar al cajero a buscar "3 pedidos" sin decirle
        /// cuales es lo mismo que no decirle nada. Si no viene —porque el rechazo lo levanto
        /// el trigger y no el controlador, que es el caso de las cuentas sin cobrar— cae a la
        /// ApiException de siempre con el mensaje del trigger.
        /// </summary>
        private static async Task<ApiException> ErrorDeCierreAsync(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                try
                {
                    var bloqueo = await response.Content.ReadFromJsonAsync<CierreBloqueadoDto>(JsonOptions);
                    if (bloqueo is not null && bloqueo.Pedidos.Count > 0)
                        return new CierreTurnoException(bloqueo.Message, bloqueo.Pedidos);
                }
                catch (JsonException)
                {
                    // El cuerpo no tenia la forma esperada: sigue al mensaje generico.
                }
            }

            return new ApiException(await LeerMensajeErrorAsync(response));
        }

        // Mismo criterio que EntityApiClient: la API devuelve { "message": "..." } para lo que
        // ya traduce; para el resto, un mensaje por status code.
        private static async Task<string> LeerMensajeErrorAsync(HttpResponseMessage response)
        {
            try
            {
                var texto = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(texto))
                {
                    using var doc = JsonDocument.Parse(texto);
                    if (doc.RootElement.TryGetProperty("message", out var mensaje) && mensaje.ValueKind == JsonValueKind.String)
                        return mensaje.GetString()!;
                }
            }
            catch (JsonException)
            {
            }

            return response.StatusCode switch
            {
                HttpStatusCode.Forbidden => "No tenés permisos para realizar esta acción.",
                HttpStatusCode.NotFound => "El turno no existe.",
                HttpStatusCode.BadRequest => "Los datos enviados no son válidos.",
                HttpStatusCode.Conflict => "La operación no se puede completar por un conflicto con datos existentes.",
                _ => $"Error inesperado del servidor ({(int)response.StatusCode}).",
            };
        }
    }
}
