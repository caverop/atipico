using Atipico.Domain.Interfaces;

namespace Atipico.Web.Services
{
    /// <summary>
    /// Decorador sobre <see cref="EntityApiClient{TEntity}"/> que reporta cada llamada a
    /// <see cref="EstadoOperaciones"/>. No cambia el contrato, así que las páginas no se
    /// enteran: cualquiera que use <see cref="IEntityApiClient{TEntity}"/> muestra la barra
    /// sin una línea de código propia.
    /// </summary>
    public class EntityApiClientConProgreso<TEntity> : IEntityApiClient<TEntity>
        where TEntity : class, IEntity
    {
        private readonly EntityApiClient<TEntity> _interno;
        private readonly EstadoOperaciones _estado;

        public EntityApiClientConProgreso(EntityApiClient<TEntity> interno, EstadoOperaciones estado)
        {
            _interno = interno;
            _estado = estado;
        }

        public Task<List<TEntity>> GetAllAsync() =>
            _estado.SeguirAsync(_interno.GetAllAsync);

        public Task<TEntity?> GetByIdAsync(long id) =>
            _estado.SeguirAsync(() => _interno.GetByIdAsync(id));

        public Task<TEntity?> CreateAsync(TEntity entity) =>
            _estado.SeguirAsync(() => _interno.CreateAsync(entity));

        public Task UpdateAsync(TEntity entity) =>
            _estado.SeguirAsync(() => _interno.UpdateAsync(entity));

        public Task DeleteAsync(long id) =>
            _estado.SeguirAsync(() => _interno.DeleteAsync(id));
    }
}
