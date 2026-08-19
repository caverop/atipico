using Atipico.Domain.Entities;
using Atipico.Infraestructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Atipico.Infraestructure.Tests
{
    // El modelo EF no se valida al compilar: un mapeo mal escrito (columna que no
    // existe, relacion mal declarada) recien falla al construir el modelo, que en
    // produccion ocurre con la primera consulta. Construirlo aca no abre conexion.
    public class ModeloComprobantePagoTest
    {
        private static AppDbContext CrearContexto()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=no_se_conecta")
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public void ElModeloCompletoSeConstruye()
        {
            using var contexto = CrearContexto();

            Assert.NotNull(contexto.Model);
        }

        [Fact]
        public void ComprobantePagoMapeaLaTablaYSusColumnas()
        {
            using var contexto = CrearContexto();
            var tipo = contexto.Model.FindEntityType(typeof(ComprobantePago));

            Assert.NotNull(tipo);
            Assert.Equal("comprobante_pago", tipo!.GetTableName());
            Assert.Equal("id_cuenta", tipo.FindProperty(nameof(ComprobantePago.IdCuenta))!.GetColumnName());
            Assert.Equal("hash_sha256", tipo.FindProperty(nameof(ComprobantePago.HashSha256))!.GetColumnName());
            Assert.Equal("storage_key", tipo.FindProperty(nameof(ComprobantePago.StorageKey))!.GetColumnName());
            Assert.Equal("id_subido_por", tipo.FindProperty(nameof(ComprobantePago.IdSubidoPor))!.GetColumnName());
            Assert.Equal("id_reemplaza", tipo.FindProperty(nameof(ComprobantePago.IdReemplaza))!.GetColumnName());
        }

        [Fact]
        public void ComprobantePagoSeAutorreferenciaParaElReemplazo()
        {
            using var contexto = CrearContexto();
            var tipo = contexto.Model.FindEntityType(typeof(ComprobantePago))!;

            var reemplazo = tipo.GetForeignKeys()
                .Single(fk => fk.Properties.Any(p => p.Name == nameof(ComprobantePago.IdReemplaza)));

            Assert.Equal(typeof(ComprobantePago), reemplazo.PrincipalEntityType.ClrType);
            // uk_comprobante_reemplaza: la cadena de reemplazos no se bifurca.
            Assert.True(reemplazo.IsUnique);
        }

        [Theory]
        [InlineData(typeof(ComprobanteVigente), "v_comprobante_vigente")]
        [InlineData(typeof(CuentaQrEvidenciaIncompleta), "v_cuenta_qr_evidencia_incompleta")]
        [InlineData(typeof(ComprobanteDuplicado), "v_comprobante_duplicado")]
        public void LasVistasSeMapeanSinClave(Type tipoEntidad, string vista)
        {
            using var contexto = CrearContexto();
            var tipo = contexto.Model.FindEntityType(tipoEntidad);

            Assert.NotNull(tipo);
            Assert.Equal(vista, tipo!.GetViewName());
            Assert.Null(tipo.FindPrimaryKey());
        }
    }
}
