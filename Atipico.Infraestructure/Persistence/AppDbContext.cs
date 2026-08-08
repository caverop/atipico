using Atipico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Infraestructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
            optionsBuilder
                .UseNpgsql(connectionString);
        }
        public DbSet<Usuario> Users => Set<Usuario>();
    }
}
