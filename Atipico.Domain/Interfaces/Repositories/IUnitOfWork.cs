using Atipico.Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Application.Common.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<TEntity> Repository<TEntity>() where TEntity : class;
        Task<int> SaveChangesAsync();
    }
}
