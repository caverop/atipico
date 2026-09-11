using Atipico.Database.Tests.Infrastructure;
using Atipico.Domain.Entities;
using Atipico.Infraestructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Atipico.Database.Tests
{
    // specs/agente-db.md §4.3.2 — la contraparte de Atipico.Infraestructure.Tests/
    // ModeloEnumsCheckTest.cs, pero contra el CATÁLOGO REAL del contenedor (pg_constraint),
    // no contra el texto de sql/*.sql. La assertion es SUBCONJUNTO, no igualdad —a propósito,
    // §4.3.2: "MetodoPago define Efectivo/Qr mientras el CHECK permite cuatro valores, y eso
    // es holgura deliberada. Un valor de más en la base es holgura; uno de menos es una
    // ruptura en producción." Lo que no puede pasar, para ningún enum, es que C# emita un
    // literal que la base termine rechazando.
    [Collection(PostgresCollection.Nombre)]
    public class EnumCheckTests
    {
        private readonly PostgresFixture _fixture;

        public EnumCheckTests(PostgresFixture fixture) => _fixture = fixture;

        // Misma tabla que ModeloEnumsCheckTest.EnumsConCheckEstricto(), más TipoPedido y
        // MetodoPago (que ahí se prueban aparte, con criterios distintos): acá el criterio es
        // uno solo — subconjunto — así que entran los ocho.
        public static TheoryData<Type, string, string> TodosLosEnumsConCheck() => new()
        {
            { typeof(Usuario),     nameof(Usuario.Rol),        "ck_usuario_rol" },
            { typeof(Plato),       nameof(Plato.Estado),       "ck_plato_estado" },
            { typeof(Mesa),        nameof(Mesa.Estado),        "ck_mesa_estado" },
            { typeof(Pedido),      nameof(Pedido.Estado),      "ck_pedido_estado" },
            { typeof(Pedido),      nameof(Pedido.Tipo),        "ck_pedido_tipo" },
            { typeof(PedidoPlato), nameof(PedidoPlato.Estado), "ck_pedido_plato_estado" },
            { typeof(Cuenta),      nameof(Cuenta.Estado),      "ck_cuenta_estado" },
            { typeof(Cuenta),      nameof(Cuenta.MetodoPago),  "ck_cuenta_metodo" },
        };

        [DockerTheory]
        [MemberData(nameof(TodosLosEnumsConCheck))]
        public async Task TodoValorDelEnumEstaPermitidoPorElCheckReal(Type entidad, string propiedad, string constraint)
        {
            var conversor = ConversorDe(entidad, propiedad);
            var tipoEnum = TipoDelEnum(entidad, propiedad);

            var permitidos = await LiteralesDelCheckRealAsync(constraint);

            foreach (var valor in Enum.GetValues(tipoEnum))
            {
                var literal = (string)conversor.ConvertToProvider(valor)!;

                Assert.True(
                    permitidos.Contains(literal),
                    $"{entidad.Name}.{propiedad} = {valor} se serializa como '{literal}', que el " +
                    $"CHECK real {constraint} (leído del contenedor, no de sql/) no permite. " +
                    $"Permitidos ahí: {string.Join(", ", permitidos)}.");
            }
        }

        private async Task<HashSet<string>> LiteralesDelCheckRealAsync(string constraint)
        {
            await using var conexion = new NpgsqlConnection(_fixture.CadenaOwnerConnectionString);
            await conexion.OpenAsync();

            await using var comando = new NpgsqlCommand(
                "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = @nombre", conexion);
            comando.Parameters.AddWithValue("nombre", constraint);

            var definicion = (string?)await comando.ExecuteScalarAsync();
            Assert.True(definicion is not null, $"No se encontró la restricción {constraint} en el contenedor.");

            // Misma extracción que ModeloEnumsCheckTest: lo que va entre comillas simples,
            // sea la forma "IN ('A','B')" o la de pg_dump "= ANY (ARRAY['A'::...])".
            return System.Text.RegularExpressions.Regex
                .Matches(definicion!, @"'([^']*)'")
                .Select(m => m.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);
        }

        private static AppDbContext CrearContexto()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=no_se_conecta")
                .Options;

            return new AppDbContext(options);
        }

        private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter
            ConversorDe(Type entidad, string propiedad)
        {
            using var contexto = CrearContexto();
            var prop = contexto.Model.FindEntityType(entidad)?.FindProperty(propiedad);

            Assert.True(prop is not null, $"{entidad.Name}.{propiedad} no está mapeada en el modelo.");

            var conversor = prop!.GetValueConverter();
            Assert.True(conversor is not null, $"{entidad.Name}.{propiedad} no tiene value converter.");

            return conversor!;
        }

        private static Type TipoDelEnum(Type entidad, string propiedad)
        {
            var tipo = entidad.GetProperty(propiedad)!.PropertyType;
            return Nullable.GetUnderlyingType(tipo) ?? tipo;
        }
    }
}
