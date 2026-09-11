using Npgsql;
using Testcontainers.PostgreSql;

namespace Atipico.Database.Tests.Infrastructure
{
    // Un contenedor por colección de tests (specs/agente-db.md §4.1), con DOS bases dentro:
    //   atipico_chain    — script_inicial.sql + 002...NNN en orden (la vía canónica real)
    //   atipico_snapshot — schema_completo.sql (el atajo de un solo archivo)
    // Vivir en el mismo contenedor y no en dos evita pagar dos arranques (~4s cada uno,
    // specs/agente-db.md §5.1) para comparar algo que de por sí es una comparación de
    // catálogo, no de carga.
    //
    // El agente es superusuario acá — cosa que contra Neon nunca fue cierta — y eso habilita
    // lo que hoy no se puede probar en ningún lado: montar el rol app_restaurante con sus
    // GRANT reales y correr las assertions DESDE esa conexión (§4.2).
    public sealed class PostgresFixture : IAsyncLifetime
    {
        private const string BaseDatosCadena = "atipico_chain";
        private const string BaseDatosSnapshot = "atipico_snapshot";

        // Contraseñas fijas, válidas solo dentro de este contenedor descartable: no son un
        // secreto real, mismo razonamiento que la contraseña local de docker-compose.db.yml
        // (specs/postgres-local-dev.md). El contenedor no sobrevive al proceso de test.
        private const string PasswordSuperusuario = "postgres";
        private const string PasswordAppRestaurante = "solo-en-este-contenedor";

        private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:18-alpine")
            .WithUsername("postgres")
            .WithPassword(PasswordSuperusuario)
            .WithDatabase(BaseDatosCadena)
            .Build();

        public bool ContenedorLevantado { get; private set; }

        public string CadenaOwnerConnectionString { get; private set; } = string.Empty;
        public string CadenaAppRestauranteConnectionString { get; private set; } = string.Empty;
        public string SnapshotOwnerConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            if (!DockerAvailability.EstaDisponible)
            {
                // No intentar levantar nada: todos los [DockerFact] de esta colección ya
                // quedaron marcados Skip en el descubrimiento. Si igual intentáramos arrancar
                // el contenedor acá, reventaría con una excepción de Docker no disponible y
                // xUnit reportaría la colección entera como Failed en vez de Skipped.
                return;
            }

            await _contenedor.StartAsync();
            ContenedorLevantado = true;

            var raizSql = Path.Combine(RaizDelRepositorio(), "sql");

            CadenaOwnerConnectionString = _contenedor.GetConnectionString();

            // Las tres cadenas se arman ANTES de usarlas: AplicarSnapshotAsync ya necesita
            // SnapshotOwnerConnectionString, y armarla después dejaba una cadena vacía en el
            // momento en que se la llamaba — se detectó corriendo la suite, no leyendo el
            // código.
            CadenaAppRestauranteConnectionString = new NpgsqlConnectionStringBuilder(CadenaOwnerConnectionString)
            {
                Username = "app_restaurante",
                Password = PasswordAppRestaurante,
            }.ConnectionString;

            SnapshotOwnerConnectionString = new NpgsqlConnectionStringBuilder(CadenaOwnerConnectionString)
            {
                Database = BaseDatosSnapshot,
            }.ConnectionString;

            await AplicarCadenaCanonicaAsync(raizSql);
            await FijarPasswordAppRestauranteAsync();
            await CrearBaseSnapshotAsync();
            await AplicarSnapshotAsync(raizSql);
        }

        public async Task DisposeAsync()
        {
            if (ContenedorLevantado)
                await _contenedor.DisposeAsync();
        }

        // script_inicial.sql + toda migración numerada (002...NNN), en orden por nombre —
        // mismo predicado que Atipico.Infraestructure.Tests/ModeloEnumsCheckTest.cs usa para
        // no duplicar el criterio de "qué archivo cuenta como migración numerada". No es una
        // lista fija: si mañana existe 016, este fixture la recoge sola.
        private async Task AplicarCadenaCanonicaAsync(string raizSql)
        {
            await using var conexion = new NpgsqlConnection(CadenaOwnerConnectionString);
            await conexion.OpenAsync();

            await EjecutarArchivoAsync(conexion, Path.Combine(raizSql, "script_inicial.sql"));

            var migraciones = Directory
                .GetFiles(raizSql, "*.sql")
                .Where(f => char.IsDigit(Path.GetFileName(f)[0]))
                .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal);

            foreach (var migracion in migraciones)
                await EjecutarArchivoAsync(conexion, migracion);
        }

        private async Task AplicarSnapshotAsync(string raizSql)
        {
            await using var conexion = new NpgsqlConnection(SnapshotOwnerConnectionString);
            await conexion.OpenAsync();
            await EjecutarArchivoAsync(conexion, Path.Combine(raizSql, "schema_completo.sql"));
        }

        private async Task FijarPasswordAppRestauranteAsync()
        {
            // script_inicial.sql BLOQUE 2 crea el rol con LOGIN pero sin password — no hace
            // falta contra un socket local de confianza, pero Testcontainers expone el
            // contenedor por TCP y el pg_hba.conf de la imagen oficial exige autenticación.
            await using var conexion = new NpgsqlConnection(CadenaOwnerConnectionString);
            await conexion.OpenAsync();

            await using var comando = new NpgsqlCommand(
                $"ALTER ROLE app_restaurante WITH PASSWORD '{PasswordAppRestaurante}';", conexion);
            await comando.ExecuteNonQueryAsync();
        }

        private async Task CrearBaseSnapshotAsync()
        {
            await using var conexion = new NpgsqlConnection(CadenaOwnerConnectionString);
            await conexion.OpenAsync();

            await using var comando = new NpgsqlCommand($"CREATE DATABASE {BaseDatosSnapshot};", conexion);
            await comando.ExecuteNonQueryAsync();
        }

        private static async Task EjecutarArchivoAsync(NpgsqlConnection conexion, string ruta)
        {
            var texto = NormalizarFinesDeLinea(await File.ReadAllTextAsync(ruta));
            var sql = QuitarMetaComandosDePsql(texto);
            await using var comando = new NpgsqlCommand(sql, conexion);
            comando.CommandTimeout = 60;

            try
            {
                await comando.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Falló aplicando {Path.GetFileName(ruta)} contra el contenedor de prueba.", ex);
            }
        }

        // `psql`/`pg_dump` modernos envuelven el volcado en `\restrict <token>` /
        // `\unrestrict <token>` (verificado en sql/schema_completo.sql:53 y :1317) — un
        // meta-comando de CLIENTE de psql, no SQL, que evita que un dump se ejecute con
        // search_path manipulado en el restore. Npgsql habla el protocolo de cable, no el
        // lenguaje de psql, y revienta con "syntax error" al toparlo. Se filtra cualquier
        // línea que empiece con `\` (tolerando espacio inicial) antes de mandar el texto —
        // ninguna migración numerada ni script_inicial.sql usa meta-comandos hoy (verificado
        // con grep sobre sql/*.sql), así que el filtro no cambia nada ahí; importa para
        // schema_completo.sql y para cualquier regeneración futura con pg_dump.
        private static string QuitarMetaComandosDePsql(string sql) =>
            string.Join('\n', sql
                .Split('\n')
                .Where(linea => !linea.TrimStart().StartsWith('\\')));

        // sql/script_inicial.sql está en un checkout de Windows con CRLF. Dentro de un
        // $function$...$function$ (dollar-quoting) Postgres guarda el texto LITERAL, sin
        // tratar \r\n como fin de línea a normalizar — así que un cuerpo de función con CRLF
        // embebido queda byte-distinto del mismo cuerpo tal como lo escribe pg_dump (LF puro)
        // en schema_completo.sql, aunque el código sea idéntico. Se detectó comparando
        // fn_cuenta_inmutable byte a byte entre las dos bases: mismo texto visible, `file`
        // marcaba un lado "with CRLF, LF line terminators" y el otro "ASCII text" a secas.
        // Normalizar ANTES de ejecutar evita que el CRLF llegue a guardarse en el catálogo.
        private static string NormalizarFinesDeLinea(string texto) =>
            texto.Replace("\r\n", "\n").Replace("\r", "\n");

        // Mismo patrón que RaizDelRepositorio() en ModeloEnumsCheckTest.cs: sube desde el
        // directorio de salida del build hasta encontrar la carpeta sql/.
        private static string RaizDelRepositorio()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "sql")))
                dir = dir.Parent;

            if (dir is null)
                throw new InvalidOperationException(
                    "No se encontró la carpeta sql/ subiendo desde " + AppContext.BaseDirectory);

            return dir.FullName;
        }
    }

    // Une los cuatro archivos de test bajo un solo contenedor compartido: xUnit crea la
    // fixture una vez por colección, no una vez por clase.
    [CollectionDefinition(Nombre)]
    public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
    {
        public const string Nombre = "Postgres descartable";
    }
}
