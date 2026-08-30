using System.Linq.Expressions;

namespace Atipico.Application.Interfaces.Services
{
    public interface IEntityService<TEntity> where TEntity : class
    {
        Task<TEntity?> GetByIdAsync(long id);
        Task<IEnumerable<TEntity>> GetAllAsync();

        /// <summary>
        /// Filtra en la base, no en memoria. IRepository ya lo ofrecia; faltaba aca, y por eso
        /// las pantallas venian trayendo la tabla entera para descartarla del lado del cliente
        /// (ver Pedidos/Index.razor). Ver docs/numero-pedido.md §7.3.
        /// </summary>
        Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);
        Task<TEntity> AddAsync(TEntity entity);
        Task UpdateAsync(TEntity entity);
        Task RemoveAsync(TEntity entity);
    }
}
