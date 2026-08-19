using Atipico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class ComprobantePagoConfiguration : IEntityTypeConfiguration<ComprobantePago>
    {
        public void Configure(EntityTypeBuilder<ComprobantePago> builder)
        {
            builder.ToTable("comprobante_pago",
                t => t.HasCheckConstraint("ck_comprobante_monto", "monto IS NULL OR monto > 0"));

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            builder.Property(c => c.Monto).HasColumnName("monto").HasPrecision(12, 2);

            builder.Property(c => c.StorageKey).HasColumnName("storage_key").IsRequired();
            builder.HasIndex(c => c.StorageKey).IsUnique().HasDatabaseName("uk_comprobante_key");

            builder.Property(c => c.HashSha256).HasColumnName("hash_sha256")
                .HasColumnType("char(64)").IsRequired();
            builder.HasIndex(c => c.HashSha256).HasDatabaseName("ix_comprobante_hash");

            builder.Property(c => c.TipoContenido).HasColumnName("tipo_contenido")
                .HasMaxLength(30).IsRequired();
            builder.Property(c => c.Bytes).HasColumnName("bytes");

            builder.Property(c => c.CreadoEn).HasColumnName("creado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAdd();

            builder.Property(c => c.MotivoReemplazo).HasColumnName("motivo_reemplazo");

            builder.Property(c => c.IdCuenta).HasColumnName("id_cuenta");
            builder.HasOne(c => c.Cuenta)
                .WithMany(cu => cu.ComprobantePagos)
                .HasForeignKey(c => c.IdCuenta)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(c => new { c.IdCuenta, c.CreadoEn }).HasDatabaseName("ix_comprobante_cuenta")
                .IsDescending(false, true);

            // Control contra reusar el mismo archivo dentro de una cuenta.
            builder.HasIndex(c => new { c.IdCuenta, c.HashSha256 }).IsUnique()
                .HasDatabaseName("uk_comprobante_cuenta_hash");

            builder.Property(c => c.IdSubidoPor).HasColumnName("id_subido_por");
            builder.HasOne(c => c.SubidoPor)
                .WithMany()
                .HasForeignKey(c => c.IdSubidoPor)
                .OnDelete(DeleteBehavior.Restrict);

            // Autorreferencia 1:0..1 — un comprobante corrige a lo sumo a otro, y
            // uk_comprobante_reemplaza impide que la cadena se bifurque.
            builder.Property(c => c.IdReemplaza).HasColumnName("id_reemplaza");
            builder.HasOne(c => c.Reemplaza)
                .WithOne()
                .HasForeignKey<ComprobantePago>(c => c.IdReemplaza)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(c => c.IdReemplaza).IsUnique()
                .HasDatabaseName("uk_comprobante_reemplaza");
        }
    }
}
