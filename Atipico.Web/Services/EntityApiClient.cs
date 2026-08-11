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

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TEntity>(JsonOptions);
        }

        public async Task<TEntity?> CreateAsync(TEntity entity)
        {
            var response = await _http.PostAsJsonAsync(_route, entity, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TEntity>(JsonOptions);
        }

        public async Task UpdateAsync(TEntity entity)
        {
            var response = await _http.PutAsJsonAsync($"{_route}/{entity.Id}", entity, JsonOptions);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteAsync(long id)
        {
            var response = await _http.DeleteAsync($"{_route}/{id}");
            response.EnsureSuccessStatusCode();
        }
    }
}
