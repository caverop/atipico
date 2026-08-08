using Atipico.Application.Common.Interfaces;
using Atipico.Domain.Interfaces.Repositories;
using Atipico.Infraestructure.Persistence.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Infraestructure.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public IRepository<TEntity> Repository<TEntity>() where TEntity : class
        {
            return new Repository<TEntity>(_context);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
