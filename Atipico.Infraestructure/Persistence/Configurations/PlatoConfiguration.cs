using Atipico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class PlatoConfiguration : IEntityTypeConfiguration<Plato>
    {
        public void Configure(EntityTypeBuilder<Plato> builder)
        {
            builder.ToTable("plato", t => t.HasCheckConstraint("ck_plato_precio", "precio >= 0"));

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            builder.Property(p => p.Nombre).HasColumnName("nombre").HasMaxLength(120).IsRequired();
            builder.Property(p => p.Precio).HasColumnName("precio").HasPrecision(10, 2);
            builder.Property(p => p.Activo).HasColumnName("activo").HasDefaultValue(true);

            builder.Property(p => p.CreadoEn).HasColumnName("creado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            builder.Property(p => p.ActualizadoEn).HasColumnName("actualizado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

            builder.Property(p => p.IdTipoPlato).HasColumnName("id_tipo_plato");
            builder.HasIndex(p => p.IdTipoPlato).HasDatabaseName("ix_plato_tipo");
        }
    }
}
