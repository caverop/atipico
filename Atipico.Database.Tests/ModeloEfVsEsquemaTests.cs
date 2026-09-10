using Atipico.Database.Tests.Infrastructure;
using Atipico.Infraestructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Atipico.Database.Tests
{
    // specs/agente-db.md §4.3.3 — AppDbContext contra el contenedor. Hoy una
    // IEntityTypeConfiguration que nombre una columna que no existe, o un tipo que no mapea,
    // se descubre recién en tiempo de ejecución de la aplicación real. Este test lo mueve a
    // CI: recorre cada DbSet por reflexión (nada que mantener a mano si se agrega uno nuevo)
    // y fuerza una consulta trivial contra la base real, incluidas las cinco vistas de solo
    // lectura (HasNoKey) — si EF y el esquema real divergen en algo, la traducción o la
    // ejecución de la consulta revientan ahí, con el nombre del DbSet en el mensaje.
    //
    // Corre con la conexión de app_restaurante, no la de superusuario: es la que usa la
    // aplicación real, y todo lo que estas consultas necesitan es SELECT.
    [Collection(PostgresCollection.Nombre)]
    public class ModeloEfVsEsquemaTests
    {
        private readonly PostgresFixture _fixture;

        public ModeloEfVsEsquemaTests(PostgresFixture fixture) => _fixture = fixture;

        [DockerFact]
        public void CadaDbSetSeConsultaSinReventarContraElEsquemaReal()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_fixture.CadenaAppRestauranteConnectionString)
                .Options;

            using var contexto = new AppDbContext(options);

            var propiedadesDbSet = typeof(AppDbContext)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(EsPropiedadDbSet)
                .ToList();

            Assert.NotEmpty(propiedadesDbSet);

            var fallas = new List<string>();

            foreach (var propiedad in propiedadesDbSet)
            {
                var tipoEntidad = propiedad.PropertyType.GetGenericArguments()[0].Name;

                try
                {
                    var dbSet = (System.Collections.IEnumerable)propiedad.GetValue(contexto)!;

                    // Basta con abrir el enumerador: fuerza a EF a traducir y ejecutar la
                    // consulta contra Postgres real. Las tablas nacen vacías (este fixture no
                    // corre dev_datos_iniciales.sql), así que recorrer todo no cuesta nada.
                    foreach (var _ in dbSet) { }
                }
                catch (Exception ex)
                {
                    fallas.Add($"{propiedad.Name} (DbSet<{tipoEntidad}>): {ex.Message}");
                }
            }

            Assert.True(fallas.Count == 0,
                "Los siguientes DbSet no se pudieron consultar contra el esquema real " +
                "(columna que no existe, tipo que no mapea, u otra deriva entre la " +
                "IEntityTypeConfiguration y sql/):\n  " + string.Join("\n  ", fallas));
        }

        private static bool EsPropiedadDbSet(PropertyInfo propiedad) =>
            propiedad.PropertyType.IsGenericType &&
            propiedad.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>);
    }
}
