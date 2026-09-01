using Atipico.Domain.Entities;
using Atipico.Infraestructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Atipico.Infraestructure.Tests
{
    // Un HasColumnName mal escrito no falla al compilar ni al construir el modelo: falla con
    // la primera consulta, en produccion. Estas columnas ademas se usan poco (solo en pedidos
    // delivery), asi que el error podria tardar dias en aparecer.
    public class ModeloPedidoEntregaTest
    {
        private static AppDbContext CrearContexto()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=no_se_conecta")
                .Options;

            return new AppDbContext(options);
        }

        [Theory]
        [InlineData(nameof(Pedido.DireccionEntrega), "direccion_entrega")]
        [InlineData(nameof(Pedido.UbicacionCompartida), "ubicacion_compartida")]
        [InlineData(nameof(Pedido.LatitudEntrega), "latitud_entrega")]
        [InlineData(nameof(Pedido.LongitudEntrega), "longitud_entrega")]
        public void LasColumnasDeEntregaSeMapeanYSonAnulables(string propiedad, string columna)
        {
            using var contexto = CrearContexto();
            var tipo = contexto.Model.FindEntityType(typeof(Pedido))!;

            var mapeada = tipo.FindProperty(propiedad);

            Assert.NotNull(mapeada);
            Assert.Equal(columna, mapeada!.GetColumnName());
            // Un pedido puede nacer sin nada de esto (specs/direccion-entrega.md §4).
            Assert.True(mapeada.IsNullable);
        }

        // numeric(9,6): 3 digitos enteros — alcanza para -180 — y 6 decimales, unos 11 cm.
        // Sin esto EF elige numeric(18,2) por defecto y trunca a dos decimales, que en
        // latitud son mas de un kilometro.
        [Theory]
        [InlineData(nameof(Pedido.LatitudEntrega))]
        [InlineData(nameof(Pedido.LongitudEntrega))]
        public void LasCoordenadasConservanSeisDecimales(string propiedad)
        {
            using var contexto = CrearContexto();
            var mapeada = contexto.Model.FindEntityType(typeof(Pedido))!.FindProperty(propiedad)!;

            Assert.Equal(9, mapeada.GetPrecision());
            Assert.Equal(6, mapeada.GetScale());
        }
    }
}
