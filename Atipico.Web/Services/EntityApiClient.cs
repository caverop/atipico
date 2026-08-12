using Atipico.Domain.Interfaces;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atipico.Web.Services
{
    public class EntityApiClient<TEntity> : IEntityApiClient<TEntity> where TEntity : class, IEntity
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _http;
        private readonly string _route;

        public EntityApiClient(IHttpClientFactory httpClientFactory)
        {
            _http = httpClientFactory.CreateClient("AtipicoApi");
            _route = ApiRoutes.For<TEntity>();
        }

        public async Task<List<TEntity>> GetAllAsync()
        {
            var result = await _http.GetFromJsonAsync<List<TEntity>>(_route, JsonOptions);
            return result ?? [];
        }

        public async Task<TEntity?> GetByIdAsync(long id)
        {
            var response = await _http.GetAsync($"{_route}/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
                throw new ApiException(await LeerMensajeErrorAsync(response));

            return await response.Content.ReadFromJsonAsync<TEntity>(JsonOptions);
        }

        public async Task<TEntity?> CreateAsync(TEntity entity)
        {
            var response = await _http.PostAsJsonAsync(_route, entity, JsonOptions);
            if (!response.IsSuccessStatusCode)
                throw new ApiException(await LeerMensajeErrorAsync(response));

            return await response.Content.ReadFromJsonAsync<TEntity>(JsonOptions);
        }

        public async Task UpdateAsync(TEntity entity)
        {
            var response = await _http.PutAsJsonAsync($"{_route}/{entity.Id}", entity, JsonOptions);
            if (!response.IsSuccessStatusCode)
                throw new ApiException(await LeerMensajeErrorAsync(response));
        }

        public async Task DeleteAsync(long id)
        {
            var response = await _http.DeleteAsync($"{_route}/{id}");
            if (!response.IsSuccessStatusCode)
                throw new ApiException(await LeerMensajeErrorAsync(response));
        }

        // La API devuelve { "message": "..." } para los errores que ya traduce
        // (ver EntityControllerBase.TryTranslateDbError); para el resto (403 de rol,
        // 400 de validación automática, etc.) cae a un mensaje genérico por status code.
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
                // El cuerpo no es el JSON { "message": ... } esperado: seguimos al mensaje genérico.
            }

            return response.StatusCode switch
            {
                HttpStatusCode.Forbidden => "No tenés permisos para realizar esta acción.",
                HttpStatusCode.NotFound => "El registro no existe o ya fue eliminado.",
                HttpStatusCode.BadRequest => "Los datos enviados no son válidos.",
                HttpStatusCode.Conflict => "La operación no se puede completar por un conflicto con datos existentes.",
                _ => $"Error inesperado del servidor ({(int)response.StatusCode}).",
            };
        }
    }
}
