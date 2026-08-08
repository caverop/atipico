using Atipico.Application.Common.Interfaces;
using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Interfaces.Repositories;

namespace Atipico.Application.Services
{
    public class EntityService<TEntity> : IEntityService<TEntity> where TEntity : class
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRepository<TEntity> _repository;

        public EntityService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _repository = unitOfWork.Repository<TEntity>();
        }

        public Task<TEntity?> GetByIdAsync(long id)
        {
            return _repository.GetByIdAsync(id);
        }

        public Task<IEnumerable<TEntity>> GetAllAsync()
        {
            return _repository.GetAllAsync();
        }

        public async Task<TEntity> AddAsync(TEntity entity)
        {
            _repository.Add(entity);
            await _unitOfWork.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(TEntity entity)
        {
            _repository.Update(entity);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task RemoveAsync(TEntity entity)
        {
            _repository.Remove(entity);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
