using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Infraestructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
    {
        public void Configure(EntityTypeBuilder<Usuario> builder)
        {
            builder.ToTable("usuario");

            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            builder.Property(u => u.Nombre).HasColumnName("nombre").HasMaxLength(120).IsRequired();

            builder.Property(u => u.NombreUsuario).HasColumnName("nombre_usuario").HasMaxLength(60).IsRequired();
            builder.HasIndex(u => u.NombreUsuario).IsUnique().HasDatabaseName("uk_usuario_nombre_usuario");

            builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(200).IsRequired();

            builder.Property(u => u.Rol).HasColumnName("rol")
                .HasConversion(new UpperSnakeCaseEnumConverter<RolUsuario>()).HasMaxLength(20).IsRequired();
            builder.Property(u => u.Activo).HasColumnName("activo").HasDefaultValue(true);

            builder.Property(u => u.CreadoEn).HasColumnName("creado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            builder.Property(u => u.ActualizadoEn).HasColumnName("actualizado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

            builder.HasMany(u => u.PedidosAtendidos)
                .WithOne(p => p.Mesero)
                .HasForeignKey(p => p.IdMesero)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(u => u.CuentasAtendidas)
                .WithOne(c => c.Mesero)
                .HasForeignKey(c => c.IdMesero)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(u => u.CuentasCobradas)
                .WithOne(c => c.Cajero)
                .HasForeignKey(c => c.IdCajero)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(u => u.CuentasAnuladas)
                .WithOne(c => c.AnuladoPor)
                .HasForeignKey(c => c.IdAnuladoPor)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(u => u.PlatosAnulados)
                .WithOne(pp => pp.AnuladoPor)
                .HasForeignKey(pp => pp.IdAnuladoPor)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
