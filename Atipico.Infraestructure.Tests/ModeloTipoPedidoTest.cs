using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Infraestructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Atipico.Infraestructure.Tests
{
    // El enum de C# y el CHECK de la base se mantienen en sync A MANO (ver el comentario de
    // mantenimiento al inicio de sql/script_inicial.sql). Una divergencia no se nota al
    // compilar ni al arrancar: falla con el primer INSERT, en produccion. Estas pruebas
    // comparan las dos fuentes de verdad, leyendo el SQL de verdad en vez de copiarlo.
    public class ModeloTipoPedidoTest
    {
        private static AppDbContext CrearContexto()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=no_se_conecta")
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public void PedidoMapeaLaColumnaTipo()
        {
            using var contexto = CrearContexto();
            var tipo = contexto.Model.FindEntityType(typeof(Pedido))!;

            var propiedad = tipo.FindProperty(nameof(Pedido.Tipo));

            Assert.NotNull(propiedad);
            Assert.Equal("tipo", propiedad!.GetColumnName());
            Assert.False(propiedad.IsNullable);
        }

        [Fact]
        public void TipoPedidoSeSerializaConLosLiteralesDeCkPedidoTipo()
        {
            using var contexto = CrearContexto();
            var propiedad = contexto.Model.FindEntityType(typeof(Pedido))!
                .FindProperty(nameof(Pedido.Tipo))!;
            var conversor = propiedad.GetValueConverter();

            Assert.NotNull(conversor);

            var serializados = Enum.GetValues<TipoPedido>()
                .Select(t => (string)conversor!.ConvertToProvider(t)!)
                .ToArray();

            // Mismo conjunto, sin importar el orden en que esten escritos en el CHECK.
            Assert.Equal(
                LiteralesDelCheck().OrderBy(v => v, StringComparer.Ordinal),
                serializados.OrderBy(v => v, StringComparer.Ordinal));
        }

        [Fact]
        public void CadaLiteralDelCheckVuelveASuValorDelEnum()
        {
            using var contexto = CrearContexto();
            var conversor = contexto.Model.FindEntityType(typeof(Pedido))!
                .FindProperty(nameof(Pedido.Tipo))!
                .GetValueConverter()!;

            foreach (var literal in LiteralesDelCheck())
            {
                var valor = conversor.ConvertFromProvider(literal);

                Assert.IsType<TipoPedido>(valor);
                Assert.Equal(literal, conversor.ConvertToProvider(valor)!.ToString());
            }
        }

        // Lee los valores directamente de sql/008_pedido_tipo.sql. Copiarlos aca a mano
        // convertiria la prueba en un tercer lugar que tambien puede quedar desactualizado.
        private static IEnumerable<string> LiteralesDelCheck()
        {
            var sql = File.ReadAllText(Path.Combine(RaizDelRepositorio(), "sql", "008_pedido_tipo.sql"));

            var match = Regex.Match(sql, @"ck_pedido_tipo\s+CHECK\s*\(\s*tipo\s+IN\s*\(([^)]*)\)");
            Assert.True(match.Success, "No se encontro la restriccion ck_pedido_tipo en el script.");

            return match.Groups[1].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => v.Trim('\''));
        }

        // El binario de pruebas vive varios niveles debajo de la raiz (bin/Debug/net10.0), y
        // esa profundidad cambia segun la configuracion: se sube hasta encontrar sql/ en vez
        // de contar carpetas.
        private static string RaizDelRepositorio()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "sql")))
                dir = dir.Parent;

            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }
}
