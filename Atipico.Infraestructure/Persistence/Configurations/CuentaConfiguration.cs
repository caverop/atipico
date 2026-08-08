using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Infraestructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class CuentaConfiguration : IEntityTypeConfiguration<Cuenta>
    {
        public void Configure(EntityTypeBuilder<Cuenta> builder)
        {
            builder.ToTable("cuenta", t => t.HasCheckConstraint("ck_cuenta_monto", "monto >= 0"));

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            builder.Property(c => c.Comensal).HasColumnName("comensal").HasMaxLength(120);

            builder.Property(c => c.Estado).HasColumnName("estado")
                .HasConversion(new UpperSnakeCaseEnumConverter<EstadoCuenta>()).HasMaxLength(20).IsRequired();

            builder.Property(c => c.MetodoPago).HasColumnName("metodo_pago")
                .HasConversion(new UpperSnakeCaseEnumConverter<MetodoPago>()).HasMaxLength(20);

            builder.Property(c => c.Monto).HasColumnName("monto").HasPrecision(12, 2).HasDefaultValue(0m);

            builder.Property(c => c.CreadoEn).HasColumnName("creado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            builder.Property(c => c.PagadoEn).HasColumnName("pagado_en");
            builder.Property(c => c.AnuladoEn).HasColumnName("anulado_en");
            builder.Property(c => c.MotivoAnulacion).HasColumnName("motivo_anulacion");

            builder.Property(c => c.IdMesero).HasColumnName("id_mesero");
            builder.HasIndex(c => c.IdMesero).HasDatabaseName("ix_cuenta_mesero");

            builder.Property(c => c.IdCajero).HasColumnName("id_cajero");
            builder.HasIndex(c => c.IdCajero).HasDatabaseName("ix_cuenta_cajero");

            builder.Property(c => c.IdAnuladoPor).HasColumnName("id_anulado_por");

            builder.HasIndex(c => c.PagadoEn).HasDatabaseName("ix_cuenta_pago");
        }
    }
}
