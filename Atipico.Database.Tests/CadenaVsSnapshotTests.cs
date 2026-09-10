using Atipico.Database.Tests.Infrastructure;
using Npgsql;
using System.Text;

namespace Atipico.Database.Tests
{
    // specs/agente-db.md §4.3.1 — el test que paga la infraestructura. Compara dos bases
    // construidas de formas distintas:
    //   A) script_inicial.sql + las migraciones numeradas, en orden — la vía canónica
    //   B) schema_completo.sql — el atajo de un solo archivo (specs/script-inicial-completo.md)
    //
    // Comparación por CATÁLOGO normalizado, no por texto de pg_dump (§4.4/§5.5): dos
    // volcados de versiones distintas de pg_dump escriben la MISMA restricción de formas
    // distintas, y comparar texto crudo da ruido cosmético (~10 diferencias falsas contra 3
    // reales, medido el 2026-09-09). pg_get_constraintdef/pg_get_triggerdef normalizan eso.
    //
    // Hoy este test FALLA, y es lo correcto (§4.3.1): sql/schema_completo.sql se regeneró el
    // 2026-08-31 y no incluye 013 (mesa_compartida_por_turno) ni 014 (rol_delivery). Falla
    // señalando exactamente esas dos diferencias — no por un error de armado del test.
    [Collection(PostgresCollection.Nombre)]
    public class CadenaVsSnapshotTests
    {
        private readonly PostgresFixture _fixture;

        public CadenaVsSnapshotTests(PostgresFixture fixture) => _fixture = fixture;

        [DockerFact]
        public async Task Restricciones_son_identicas_entre_cadena_y_snapshot()
        {
            var (cadena, snapshot) = await ObtenerParAsync(
                "SELECT conname || ' :: ' || pg_get_constraintdef(oid) " +
                "FROM pg_constraint WHERE connamespace = 'public'::regnamespace " +
                "ORDER BY conname");

            AssertMismoConjunto("restricciones (CHECK/UNIQUE/FK/PK)", cadena, snapshot);
        }

        [DockerFact]
        public async Task Indices_son_identicos_entre_cadena_y_snapshot()
        {
            var (cadena, snapshot) = await ObtenerParAsync(
                "SELECT indexname || ' :: ' || indexdef " +
                "FROM pg_indexes WHERE schemaname = 'public' " +
                "ORDER BY indexname");

            AssertMismoConjunto("índices", cadena, snapshot);
        }

        [DockerFact]
        public async Task Columnas_son_identicas_entre_cadena_y_snapshot()
        {
            var (cadena, snapshot) = await ObtenerParAsync(
                "SELECT table_name || '.' || column_name || ' :: ' || data_type || " +
                "  ' nullable=' || is_nullable || ' default=' || coalesce(column_default, '<ninguno>') " +
                "FROM information_schema.columns WHERE table_schema = 'public' " +
                "ORDER BY table_name, column_name");

            AssertMismoConjunto("columnas", cadena, snapshot);
        }

        [DockerFact]
        public async Task Funciones_son_identicas_entre_cadena_y_snapshot()
        {
            var (cadena, snapshot) = await ObtenerParAsync(
                "SELECT proname || ' :: ' || pg_get_functiondef(oid) " +
                "FROM pg_proc WHERE pronamespace = 'public'::regnamespace " +
                "ORDER BY proname");

            AssertMismoConjunto("funciones", cadena, snapshot);
        }

        [DockerFact]
        public async Task Triggers_son_identicos_entre_cadena_y_snapshot()
        {
            var (cadena, snapshot) = await ObtenerParAsync(
                "SELECT tgname || ' :: ' || pg_get_triggerdef(oid) " +
                "FROM pg_trigger WHERE NOT tgisinternal " +
                "ORDER BY tgname");

            AssertMismoConjunto("triggers", cadena, snapshot);
        }

        private async Task<(HashSet<string> Cadena, HashSet<string> Snapshot)> ObtenerParAsync(string consulta)
        {
            var cadena = await ConsultarConjuntoAsync(_fixture.CadenaOwnerConnectionString, consulta);
            var snapshot = await ConsultarConjuntoAsync(_fixture.SnapshotOwnerConnectionString, consulta);
            return (cadena, snapshot);
        }

        private static async Task<HashSet<string>> ConsultarConjuntoAsync(string connectionString, string consulta)
        {
            var resultado = new HashSet<string>(StringComparer.Ordinal);

            await using var conexion = new NpgsqlConnection(connectionString);
            await conexion.OpenAsync();

            await using var comando = new NpgsqlCommand(consulta, conexion);
            await using var lector = await comando.ExecuteReaderAsync();

            while (await lector.ReadAsync())
                resultado.Add(Normalizar(lector.GetString(0)));

            return resultado;
        }

        // Esto se aprendió corriéndolo, no razonándolo (specs/agente-db.md §4.4/§5.5):
        // pg_get_constraintdef/pg_get_indexdef NO colapsan `IN ('A','B')` y
        // `= ANY (ARRAY['A','B']::tipo[])` a una forma común — Postgres guarda el árbol de
        // expresión tal como se escribió el DDL original, y script_inicial.sql usa `IN (...)`
        // mientras schema_completo.sql, por ser salida literal de pg_dump, siempre usa
        // `= ANY (ARRAY[...])`. Sin esto, las 8 restricciones de lista fallan por sintaxis
        // aunque los valores permitidos sean idénticos, y ahogan la única diferencia real
        // (ck_usuario_rol sin DELIVERY) en puro ruido cosmético.
        //
        // Normaliza en dos partes independientes del orden: el conjunto de literales
        // ('A','B',...) ordenado, y el resto de la definición con cada literal reemplazado
        // por `?` y el contenedor entero (`IN (...)` o `= ANY (ARRAY[...]::tipo[])`)
        // colapsado a un token fijo.
        // El literal viene en DOS formas según quién lo escribió: `'MESERO'::character varying`
        // (script_inicial.sql, IN plano) o `('MESERO'::character varying)::text` — con
        // paréntesis extra y un SEGUNDO cast encima — cuando pg_dump vuelca un
        // `= ANY (ARRAY[...])`. Sin el paréntesis y el segundo cast opcionales acá, el lado
        // del snapshot queda con "(?)::text" en vez de "?" y el contenedor de abajo nunca
        // matchea — se detectó corriendo la suite, la primera versión solo cubría un cast.
        private static readonly System.Text.RegularExpressions.Regex PatronLiteral =
            new(@"\(?\s*'[^']*'(?:\s*::\s*[\w\s]+)?\s*\)?(?:\s*::\s*[\w\s]+)?",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // El cast del array entero aparece en dos posiciones distintas según quién lo
        // escribió: la cadena lo envuelve en un paréntesis de más antes de castear —
        // `ANY ((ARRAY[...])::text[])`—, el snapshot no lo necesita porque cada elemento ya
        // viene casteado individualmente — `ANY (ARRAY[(?)::text, ...])`. El `\)?` antes del
        // cast opcional tolera ese paréntesis de más sin exigirlo.
        private static readonly System.Text.RegularExpressions.Regex PatronContenedorDeLista =
            new(@"(?:IN\s*\(\s*(?:\?\s*,?\s*)*\)|=\s*ANY\s*\(+\s*ARRAY\s*\[\s*(?:\?\s*,?\s*)*\]\s*\)?\s*(?:::\s*[\w\s]+\[\])?\s*\)*)",
                System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static string Normalizar(string definicion)
        {
            var literales = System.Text.RegularExpressions.Regex
                .Matches(definicion, @"'([^']*)'")
                .Select(m => m.Groups[1].Value)
                .OrderBy(v => v, StringComparer.Ordinal);

            var sinLiterales = PatronLiteral.Replace(definicion, "?");
            var esqueleto = PatronContenedorDeLista.Replace(sinLiterales, "<LISTA>");

            // Tercer y último tipo de ruido, distinto de los dos de arriba: schema_completo.sql
            // trae líneas en blanco entre sentencias de un cuerpo de función que
            // script_inicial.sql ya no tiene — semánticamente inerte en PL/pgSQL (se verificó
            // byte a byte con fn_cuenta_inmutable/fn_touch/etc., idénticas salvo por esto), pero
            // rompe una comparación de texto. Se colapsa TODO run de espacio en blanco a uno
            // solo — ya con los literales afuera (paso anterior), no hay literal cuyo contenido
            // dependa de la cantidad de espacios que esto pudiera alterar.
            esqueleto = System.Text.RegularExpressions.Regex.Replace(esqueleto, @"\s+", " ").Trim();

            return esqueleto + " || literales=[" + string.Join(",", literales) + "]";
        }

        private static void AssertMismoConjunto(string categoria, HashSet<string> cadena, HashSet<string> snapshot)
        {
            if (cadena.SetEquals(snapshot))
                return;

            var soloEnCadena = cadena.Except(snapshot).OrderBy(s => s, StringComparer.Ordinal).ToList();
            var soloEnSnapshot = snapshot.Except(cadena).OrderBy(s => s, StringComparer.Ordinal).ToList();

            var mensaje = new StringBuilder();
            mensaje.AppendLine($"La cadena canónica y schema_completo.sql difieren en {categoria}.");

            if (soloEnCadena.Count > 0)
            {
                mensaje.AppendLine($"  Solo en la cadena (script_inicial.sql + migraciones), no en el snapshot:");
                foreach (var linea in soloEnCadena)
                    mensaje.AppendLine($"    + {linea}");
            }

            if (soloEnSnapshot.Count > 0)
            {
                mensaje.AppendLine($"  Solo en schema_completo.sql, no en la cadena:");
                foreach (var linea in soloEnSnapshot)
                    mensaje.AppendLine($"    - {linea}");
            }

            Assert.Fail(mensaje.ToString());
        }
    }
}
