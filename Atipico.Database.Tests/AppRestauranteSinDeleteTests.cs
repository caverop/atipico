using Atipico.Database.Tests.Infrastructure;
using Npgsql;

namespace Atipico.Database.Tests
{
    // specs/agente-db.md §4.2 y criterio de aceptación §8 — lo primero que esta suite prueba
    // que hoy no se prueba en ningún lado: que app_restaurante realmente no puede DELETE.
    // CLAUDE.md lo afirma ("se anula, no se borra") y toda la arquitectura de anulación en
    // vez de borrado depende de que sea cierto — pero contra Neon el agente nunca es
    // superusuario, así que nadie lo había podido confirmar corriendo código real. Acá sí:
    // el fixture crea el rol con el mismo BLOQUE 2 de script_inicial.sql, sin atajos.
    [Collection(PostgresCollection.Nombre)]
    public class AppRestauranteSinDeleteTests
    {
        private const string CodigoInsufficientPrivilege = "42501";

        private readonly PostgresFixture _fixture;

        public AppRestauranteSinDeleteTests(PostgresFixture fixture) => _fixture = fixture;

        [DockerFact]
        public async Task AppRestauranteNoPuedeBorrarFilas()
        {
            await using var conexion = new NpgsqlConnection(_fixture.CadenaAppRestauranteConnectionString);
            await conexion.OpenAsync();

            await using var transaccion = await conexion.BeginTransactionAsync();

            // id = -1 nunca existe: no hace falta ninguna fila real para probar el privilegio.
            // Postgres evalúa el GRANT antes de tocar cualquier fila, así que esto revienta
            // con insufficient_privilege pase lo que pase con el WHERE.
            await using var comando = new NpgsqlCommand("DELETE FROM mesa WHERE id = -1;", conexion, transaccion);

            var excepcion = await Assert.ThrowsAsync<PostgresException>(
                () => comando.ExecuteNonQueryAsync());

            Assert.Equal(CodigoInsufficientPrivilege, excepcion.SqlState);

            await transaccion.RollbackAsync();
        }

        [DockerFact]
        public async Task AppRestauranteSiPuedeInsertarYActualizar()
        {
            // El contraste importa: no es que el rol esté roto, es que le falta *justo*
            // DELETE (y TRUNCATE). Confirma que INSERT/UPDATE, que la app sí usa todo el
            // tiempo, no se vieron afectados por el mismo REVOKE.
            await using var conexion = new NpgsqlConnection(_fixture.CadenaAppRestauranteConnectionString);
            await conexion.OpenAsync();

            await using var transaccion = await conexion.BeginTransactionAsync();

            await using (var insertar = new NpgsqlCommand(
                "INSERT INTO tipo_plato (nombre) VALUES ('probe-database-tests') RETURNING id;",
                conexion, transaccion))
            {
                var id = await insertar.ExecuteScalarAsync();
                Assert.NotNull(id);

                await using var actualizar = new NpgsqlCommand(
                    "UPDATE tipo_plato SET nombre = 'probe-actualizado' WHERE id = @id;", conexion, transaccion);
                actualizar.Parameters.AddWithValue("id", id!);

                var filas = await actualizar.ExecuteNonQueryAsync();
                Assert.Equal(1, filas);
            }

            // Se descarta: la prueba no deja datos, coherente con cómo se verificó 015 en
            // esta misma sesión.
            await transaccion.RollbackAsync();
        }
    }
}
