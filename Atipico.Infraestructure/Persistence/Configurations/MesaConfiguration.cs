using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Infraestructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class MesaConfiguration : IEntityTypeConfiguration<Mesa>
    {
        public void Configure(EntityTypeBuilder<Mesa> builder)
        {
            builder.ToTable("mesa", t => t.HasCheckConstraint("ck_mesa_cap", "capacidad > 0"));

            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            builder.Property(m => m.Numero).HasColumnName("numero");
            builder.HasIndex(m => m.Numero).IsUnique().HasDatabaseName("uk_mesa_numero");

            builder.Property(m => m.Capacidad).HasColumnName("capacidad");

            builder.Property(m => m.Estado).HasColumnName("estado")
                .HasConversion(new UpperSnakeCaseEnumConverter<EstadoMesa>()).HasMaxLength(20).IsRequired();

            builder.Property(m => m.CreadoEn).HasColumnName("creado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        }
    }
}
