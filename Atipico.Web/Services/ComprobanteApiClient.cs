using Atipico.Domain.Entities;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atipico.Web.Services
{
    public class ComprobanteApiClient : IComprobanteApiClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private const string Ruta = "comprobantes";

        private readonly HttpClient _http;

        public ComprobanteApiClient(IHttpClientFactory httpClientFactory)
        {
            _http = httpClientFactory.CreateClient("AtipicoApi");
        }

        public async Task<List<ComprobantePago>> GetPorCuentaAsync(long idCuenta)
        {
            var response = await _http.GetAsync($"{Ruta}/cuenta/{idCuenta}");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return [];

            if (!response.IsSuccessStatusCode)
                throw new ApiException(await LeerMensajeErrorAsync(response));

            return await response.Content.ReadFromJsonAsync<List<ComprobantePago>>(JsonOptions) ?? [];
        }

        public async Task<string?> GetUrlAsync(long idComprobante)
        {
            var response = await _http.GetAsync($"{Ruta}/{idComprobante}/url");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
                throw new ApiException(await LeerMensajeErrorAsync(response));

            var cuerpo = await response.Content.ReadFromJsonAsync<RespuestaUrl>(JsonOptions);
            return cuerpo?.Url;
        }

        public async Task<ComprobantePago?> RegistrarAsync(
            long idCuenta,
            Stream contenido,
            string nombreArchivo,
            string tipoContenido,
            long? idReemplaza = null,
            string? motivoReemplazo = null)
        {
            using var form = new MultipartFormDataContent();

            form.Add(new StringContent(idCuenta.ToString(CultureInfo.InvariantCulture)), "IdCuenta");

            if (idReemplaza is not null)
            {
                form.Add(new StringContent(idReemplaza.Value.ToString(CultureInfo.InvariantCulture)), "IdReemplaza");
                form.Add(new StringContent(motivoReemplazo ?? string.Empty), "MotivoReemplazo");
            }

            var archivo = new StreamContent(contenido);
            archivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(tipoContenido);
            form.Add(archivo, "Archivo", nombreArchivo);

            var response = await _http.PostAsync(Ruta, form);
            if (!response.IsSuccessStatusCode)
                throw new ApiException(await LeerMensajeErrorAsync(response));

            return await response.Content.ReadFromJsonAsync<ComprobantePago>(JsonOptions);
        }

        private sealed class RespuestaUrl
        {
            public string? Url { get; set; }
            public DateTimeOffset ExpiraEn { get; set; }
        }

        // Mismo contrato { "message": "..." } que arma ApiControllerBase.TryTranslateDbError.
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
                // El cuerpo no es el JSON esperado: seguimos al mensaje generico.
            }

            return response.StatusCode switch
            {
                HttpStatusCode.Forbidden => "No tenés permisos para registrar comprobantes.",
                HttpStatusCode.NotFound => "El comprobante no existe.",
                HttpStatusCode.BadRequest => "El archivo o los datos enviados no son válidos.",
                HttpStatusCode.Conflict => "La operación no se puede completar por un conflicto con datos existentes.",
                HttpStatusCode.RequestEntityTooLarge => "La imagen es demasiado grande.",
                _ => $"Error inesperado del servidor ({(int)response.StatusCode}).",
            };
        }
    }
}
