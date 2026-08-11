using Atipico.Domain.Interfaces;

namespace Atipico.Web.Services
{
    public interface IEntityApiClient<TEntity> where TEntity : class, IEntity
    {
        Task<List<TEntity>> GetAllAsync();
        Task<TEntity?> GetByIdAsync(long id);
        Task<TEntity?> CreateAsync(TEntity entity);
        Task UpdateAsync(TEntity entity);
        Task DeleteAsync(long id);
    }
}
