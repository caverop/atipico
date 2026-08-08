using Atipico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class TipoPlatoConfiguration : IEntityTypeConfiguration<TipoPlato>
    {
        public void Configure(EntityTypeBuilder<TipoPlato> builder)
        {
            builder.ToTable("tipo_plato");

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            builder.Property(t => t.Nombre).HasColumnName("nombre").HasMaxLength(80).IsRequired();
            builder.HasIndex(t => t.Nombre).IsUnique().HasDatabaseName("uk_tipo_plato_nombre");

            builder.Property(t => t.Observacion).HasColumnName("observacion");

            builder.Property(t => t.CreadoEn).HasColumnName("creado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAdd();

            builder.HasMany(t => t.Platos)
                .WithOne(p => p.TipoPlato)
                .HasForeignKey(p => p.IdTipoPlato)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
