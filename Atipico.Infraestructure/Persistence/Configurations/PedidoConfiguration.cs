using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Infraestructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
    {
        public void Configure(EntityTypeBuilder<Pedido> builder)
        {
            builder.ToTable("pedido");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            // Turno de caja (sql/010_turno_caja.sql, docs/numero-pedido.md §6.3). Las dos
            // columnas las asigna tg_pedido_numero_turno en el INSERT, asi que van como
            // generadas por la base: Npgsql las excluye del INSERT y las trae de vuelta con
            // RETURNING.
            //
            // Los dos SaveBehavior no son adorno; sin ellos RN-8 se rompe de dos maneras
            // distintas, y ninguna de las dos se ve al compilar:
            //
            //   BeforeSave = Ignore  ValueGeneratedOnAdd por si solo significa "la base pone
            //                        un valor SI la aplicacion no puso ninguno". Un POST con
            //                        {"numeroTurno": 77} tiene valor, asi que EF lo manda y
            //                        deja de pedir RETURNING para esa columna. El trigger igual
            //                        lo pisa —verificado, la fila queda con el correlativo
            //                        correcto— pero la entidad en memoria se queda con 77 y esa
            //                        es la que se devuelve en el 201. El cliente se lleva un
            //                        numero que no existe. Con Ignore, EF nunca manda el valor.
            //   AfterSave  = Ignore  cierra el otro lado: un PUT con otro numero no se propaga.
            //                        El numero es inmutable (RN-4).
            builder.Property(p => p.NumeroTurno).HasColumnName("numero_turno").ValueGeneratedOnAdd();
            builder.Property(p => p.NumeroTurno).Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            builder.Property(p => p.NumeroTurno).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            builder.Property(p => p.IdTurnoCaja).HasColumnName("id_turno_caja").ValueGeneratedOnAdd();
            builder.Property(p => p.IdTurnoCaja).Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            builder.Property(p => p.IdTurnoCaja).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            builder.HasOne(p => p.TurnoCaja).WithMany(t => t.Pedidos)
                .HasForeignKey(p => p.IdTurnoCaja)
                .HasConstraintName("fk_pedido_turno_caja")
                .OnDelete(DeleteBehavior.Restrict);

            // uk_pedido_numero_turno: el par es unico, no el numero solo — cada turno
            // reinicia en 1.
            builder.HasIndex(p => new { p.IdTurnoCaja, p.NumeroTurno })
                .IsUnique()
                .HasDatabaseName("uk_pedido_numero_turno");

            builder.Property(p => p.Comensal).HasColumnName("comensal").HasMaxLength(120);

            // uk_pedido_comensal_activo (sql/003_pedido_comensal_unico.sql, reemplazado por
            // sql/012_pedido_unicidad_por_turno.sql): dentro de UN TURNO, dos pedidos
            // Abierto/EnPreparacion no pueden compartir comensal. El mismo nombre vuelve a
            // estar libre en el turno siguiente.
            builder.HasIndex(p => new { p.IdTurnoCaja, p.Comensal })
                .IsUnique()
                .HasDatabaseName("uk_pedido_comensal_activo")
                .HasFilter("estado IN ('ABIERTO', 'EN_PREPARACION')");

            builder.Property(p => p.Estado).HasColumnName("estado")
                .HasConversion(new UpperSnakeCaseEnumConverter<EstadoPedido>()).HasMaxLength(20).IsRequired();

            // ck_pedido_tipo (sql/008_pedido_tipo.sql). El DEFAULT 'EN_SALON' vive en la
            // base para las filas que ya existian; aca no se declara HasDefaultValue porque
            // la entidad siempre manda un valor explicito.
            builder.Property(p => p.Tipo).HasColumnName("tipo")
                .HasConversion(new UpperSnakeCaseEnumConverter<TipoPedido>()).HasMaxLength(20).IsRequired();

            // Entrega (sql/009_pedido_direccion_entrega.sql). HasPrecision(9, 6) refleja el
            // numeric(9,6) de la base: 3 digitos enteros y 6 decimales, unos 11 cm.
            builder.Property(p => p.DireccionEntrega).HasColumnName("direccion_entrega");
            builder.Property(p => p.UbicacionCompartida).HasColumnName("ubicacion_compartida");
            builder.Property(p => p.LatitudEntrega).HasColumnName("latitud_entrega").HasPrecision(9, 6);
            builder.Property(p => p.LongitudEntrega).HasColumnName("longitud_entrega").HasPrecision(9, 6);

            builder.Property(p => p.CreadoEn).HasColumnName("creado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            builder.Property(p => p.ActualizadoEn).HasColumnName("actualizado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
            builder.Property(p => p.CerradoEn).HasColumnName("cerrado_en");

            builder.Property(p => p.IdMesero).HasColumnName("id_mesero");
            builder.HasIndex(p => p.IdMesero).HasDatabaseName("ix_pedido_mesero");
            builder.HasIndex(p => p.CreadoEn).HasDatabaseName("ix_pedido_fecha");
        }
    }
}
