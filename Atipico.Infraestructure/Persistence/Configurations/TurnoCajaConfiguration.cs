using Atipico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    // sql/010_turno_caja.sql. Ver specs/numero-pedido.md §6.3.
    public class TurnoCajaConfiguration : IEntityTypeConfiguration<TurnoCaja>
    {
        public void Configure(EntityTypeBuilder<TurnoCaja> builder)
        {
            builder.ToTable("turno_caja", t =>
            {
                t.HasCheckConstraint("ck_turno_caja_nombre", "btrim(nombre) <> ''");
                t.HasCheckConstraint("ck_turno_caja_cierre", "cerrado_en IS NULL OR cerrado_en >= abierto_en");
            });

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            builder.Property(t => t.Nombre).HasColumnName("nombre").HasMaxLength(40).IsRequired();

            builder.Property(t => t.AbiertoEn).HasColumnName("abierto_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            builder.Property(t => t.CerradoEn).HasColumnName("cerrado_en");

            // La escribe tg_pedido_numero_turno por fuera de EF, una vez por pedido creado.
            // ValueGeneratedOnAddOrUpdate hace que Npgsql la excluya del INSERT y del UPDATE y
            // la traiga de vuelta con RETURNING: si la aplicacion la enviara, sobrescribiria el
            // contador con un valor viejo y dos pedidos terminarian con el mismo numero.
            // Mismo trato que ActualizadoEn con fn_touch.
            builder.Property(t => t.UltimoNumero).HasColumnName("ultimo_numero")
                .ValueGeneratedOnAddOrUpdate();

            builder.Property(t => t.IdCajero).HasColumnName("id_cajero");
            builder.HasOne(t => t.Cajero).WithMany()
                .HasForeignKey(t => t.IdCajero)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(t => t.AbiertoEn).HasDatabaseName("ix_turno_caja_abierto_en");

            // uk_turno_caja_abierto: como maximo un turno con cerrado_en NULL. El indice real
            // es sobre la expresion constante ((true)) filtrada por cerrado_en IS NULL, que EF
            // no sabe expresar; se declara sobre CerradoEn con el mismo filtro para que el
            // modelo conozca el nombre de la restriccion y TryTranslateDbError pueda traducirla.
            // La aplicacion no crea el esquema (las migraciones son SQL a mano), asi que esta
            // aproximacion no se materializa en ningun CREATE INDEX.
            builder.HasIndex(t => t.CerradoEn)
                .IsUnique()
                .HasDatabaseName("uk_turno_caja_abierto")
                .HasFilter("cerrado_en IS NULL");
        }
    }
}
