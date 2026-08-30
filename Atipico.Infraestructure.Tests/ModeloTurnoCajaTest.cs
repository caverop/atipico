using Atipico.Domain.Entities;
using Atipico.Infraestructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Text.RegularExpressions;

namespace Atipico.Infraestructure.Tests
{
    // Mismo criterio que ModeloTipoPedidoTest: el esquema vive en sql/ escrito a mano y el
    // modelo de EF lo describe por separado. Las dos fuentes se mantienen en sync A MANO, y
    // una divergencia no falla al compilar ni al arrancar — falla con el primer INSERT.
    // Estas pruebas leen el SQL de verdad en vez de copiarlo.
    //
    // Lo que NO se puede verificar aca es el comportamiento en ejecucion: que el trigger
    // asigne el correlativo sin huecos bajo concurrencia, que el cierre se bloquee, que
    // RETURNING traiga los valores. Eso necesita una base real y esta verificado a mano
    // contra PostgreSQL 17 (docs/numero-pedido.md §10, CA-1 a CA-20).
    public class ModeloTurnoCajaTest
    {
        private static AppDbContext CrearContexto()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=no_se_conecta")
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public void TurnoCajaMapeaTodasLasColumnasDelScript()
        {
            using var contexto = CrearContexto();
            var tipo = contexto.Model.FindEntityType(typeof(TurnoCaja))!;

            Assert.Equal("turno_caja", tipo.GetTableName());

            var mapeadas = tipo.GetProperties().Select(p => p.GetColumnName()).OrderBy(c => c, StringComparer.Ordinal);

            Assert.Equal(ColumnasDeTurnoCaja().OrderBy(c => c, StringComparer.Ordinal), mapeadas);
        }

        [Fact]
        public void PedidoMapeaLasDosColumnasNuevas()
        {
            using var contexto = CrearContexto();
            var tipo = contexto.Model.FindEntityType(typeof(Pedido))!;

            var mapeadas = tipo.GetProperties().Select(p => p.GetColumnName()).ToHashSet(StringComparer.Ordinal);

            foreach (var columna in ColumnasAgregadasAPedido())
                Assert.Contains(columna, mapeadas);
        }

        // RN-8: ni la aplicacion ni la API envian estas columnas. Se verifica en las dos
        // direcciones porque cada una se rompe distinto, y ninguna de las dos se ve al
        // compilar:
        //
        //   BeforeSave  ValueGeneratedOnAdd por si solo significa "la base pone un valor SI la
        //               aplicacion no puso ninguno". Un POST con {"numeroTurno": 77} SI trae
        //               valor, asi que EF lo manda y deja de pedir RETURNING para esa columna.
        //               El trigger igual lo pisa y la fila queda bien, pero la entidad en
        //               memoria se queda con 77 y esa es la que vuelve en el 201: el cliente
        //               se lleva un numero que no existe. Pasó de verdad al implementar esto.
        //   AfterSave   sin esto, un PUT con otro numero se propaga y el numero deja de ser
        //               inmutable (RN-4).
        [Theory]
        [InlineData(nameof(Pedido.NumeroTurno), "numero_turno")]
        [InlineData(nameof(Pedido.IdTurnoCaja), "id_turno_caja")]
        public void LaAplicacionNuncaEscribeElCorrelativoNiSuTurno(string propiedad, string columna)
        {
            using var contexto = CrearContexto();
            var p = contexto.Model.FindEntityType(typeof(Pedido))!.FindProperty(propiedad)!;

            Assert.Equal(columna, p.GetColumnName());
            Assert.Equal(ValueGenerated.OnAdd, p.ValueGenerated);
            Assert.Equal(PropertySaveBehavior.Ignore, p.GetBeforeSaveBehavior());
            Assert.Equal(PropertySaveBehavior.Ignore, p.GetAfterSaveBehavior());
        }

        // ultimo_numero lo administra tg_pedido_numero_turno, una vez por pedido creado. Si EF
        // lo enviara en un UPDATE del turno, sobrescribiria el contador con un valor viejo y
        // dos pedidos terminarian con el mismo numero.
        [Fact]
        public void UltimoNumeroLoEscribeSoloLaBase()
        {
            using var contexto = CrearContexto();
            var p = contexto.Model.FindEntityType(typeof(TurnoCaja))!
                .FindProperty(nameof(TurnoCaja.UltimoNumero))!;

            Assert.Equal("ultimo_numero", p.GetColumnName());
            Assert.Equal(ValueGenerated.OnAddOrUpdate, p.ValueGenerated);
        }

        // RN-4: unico el PAR, no el numero solo — cada turno reinicia en 1. El nombre importa
        // porque es el que llega en PostgresException.ConstraintName y el que
        // ApiControllerBase.DescribirRestriccion traduce al español.
        [Fact]
        public void ElParTurnoNumeroEsUnicoYConservaElNombreDeLaRestriccion()
        {
            using var contexto = CrearContexto();
            var tipo = contexto.Model.FindEntityType(typeof(Pedido))!;

            var indice = tipo.GetIndexes()
                .SingleOrDefault(i => i.GetDatabaseName() == "uk_pedido_numero_turno");

            Assert.NotNull(indice);
            Assert.True(indice!.IsUnique);
            Assert.Equal(
                new[] { nameof(Pedido.IdTurnoCaja), nameof(Pedido.NumeroTurno) },
                indice.Properties.Select(p => p.Name));

            Assert.Contains("uk_pedido_numero_turno", Script());
        }

        // RN-3. El indice real es sobre la expresion constante ((true)) filtrada por
        // cerrado_en IS NULL, que EF no sabe expresar; lo que se verifica es que el modelo
        // conozca el nombre, no que sepa recrear el indice — la aplicacion no crea el esquema.
        [Fact]
        public void SoloPuedeHaberUnTurnoAbierto()
        {
            using var contexto = CrearContexto();

            var indice = contexto.Model.FindEntityType(typeof(TurnoCaja))!.GetIndexes()
                .SingleOrDefault(i => i.GetDatabaseName() == "uk_turno_caja_abierto");

            Assert.NotNull(indice);
            Assert.True(indice!.IsUnique);
            Assert.Equal("cerrado_en IS NULL", indice.GetFilter());

            Assert.Matches(@"CREATE UNIQUE INDEX\s+uk_turno_caja_abierto", Script());
        }

        // sql/012: la unicidad del comensal pasó a ser por turno. Antes el índice era sobre
        // (comensal) a secas, así que un nombre usado en un turno seguía bloqueado en el
        // siguiente. El NOMBRE de la restricción no cambió a propósito: es el que llega en
        // PostgresException.ConstraintName y el que DescribirRestriccion traduce.
        [Fact]
        public void ElComensalEsUnicoDentroDelTurnoYNoEnTodaLaTabla()
        {
            using var contexto = CrearContexto();

            var indice = contexto.Model.FindEntityType(typeof(Pedido))!.GetIndexes()
                .SingleOrDefault(i => i.GetDatabaseName() == "uk_pedido_comensal_activo");

            Assert.NotNull(indice);
            Assert.True(indice!.IsUnique);
            Assert.Equal(
                new[] { nameof(Pedido.IdTurnoCaja), nameof(Pedido.Comensal) },
                indice.Properties.Select(p => p.Name));
            Assert.Equal("estado IN ('ABIERTO', 'EN_PREPARACION')", indice.GetFilter());

            var sql = SinComentarios(Script("012_pedido_unicidad_por_turno.sql"));
            Assert.Contains("DROP INDEX uk_pedido_comensal_activo", sql);
            Assert.Matches(@"ON pedido \(id_turno_caja, comensal\)", sql);
        }

        // La mesa no se puede resolver con un índice: el predicado necesita pedido.estado y
        // pedido.id_turno_caja, y un índice sobre pedido_mesa solo ve columnas de pedido_mesa.
        // Vive en un trigger, y tiene que cubrir también el UPDATE: pedido_mesa se expone por
        // el CRUD genérico, así que un PUT puede mover la fila a otra mesa sin pasar por el
        // INSERT.
        [Fact]
        public void LaMesaOcupadaLaRechazaUnTriggerEnInsertYEnUpdate()
        {
            var sql = SinComentarios(Script("012_pedido_unicidad_por_turno.sql"));

            Assert.Matches(@"CREATE TRIGGER\s+tg_pedido_mesa_ocupada\s+BEFORE INSERT OR UPDATE ON pedido_mesa", sql);
            Assert.Contains("fn_pedido_mesa_ocupada", sql);
            // El lock sobre la fila de la mesa es lo que impide que dos meseros sienten gente
            // en la misma mesa a la vez: sin él los dos pasan el conteo y la doble ocupación
            // entra igual.
            Assert.Contains("FOR UPDATE", sql);
            // Sin ERRCODE propio: si no, el error sale como 500 en vez de 409 traducido.
            Assert.DoesNotContain("ERRCODE", sql);
        }

        // El correlativo lo asigna un trigger, no un DEFAULT ni una secuencia. Si alguien
        // quitara el trigger del script, todo lo demas seguiria compilando y las columnas
        // entrarian en 0 sin que nada avise.
        [Fact]
        public void ElScriptInstalaElTriggerQueAsignaElCorrelativo()
        {
            var sql = Script();

            Assert.Matches(@"CREATE TRIGGER\s+tg_pedido_numero_turno\s+BEFORE INSERT ON pedido", sql);
            Assert.Contains("fn_pedido_numero_turno", sql);
        }

        // Los mensajes de los triggers salen tal cual en la pantalla del cajero. Eso solo pasa
        // si el SQLSTATE es P0001, que es el unico codigo de trigger que TryTranslateDbError
        // traduce; un ERRCODE propio caeria en el _ => null del switch y saldria un 500 con
        // stack trace. RAISE EXCEPTION sin ERRCODE deja P0001, asi que la prueba es que no
        // aparezca ningun ERRCODE en los scripts del turno.
        [Fact]
        public void LosTriggersDelTurnoNoFijanUnErrcodePropio()
        {
            Assert.DoesNotContain("ERRCODE", SinComentarios(Script()));
            Assert.DoesNotContain("ERRCODE", SinComentarios(Script("011_turno_cierre_cuentas.sql")));
        }

        private static IEnumerable<string> ColumnasDeTurnoCaja()
        {
            var sql = SinComentarios(Script());

            var match = Regex.Match(sql, @"CREATE TABLE turno_caja \((.*?)\n\);", RegexOptions.Singleline);
            Assert.True(match.Success, "No se encontro el CREATE TABLE turno_caja en el script.");

            // Se corta por comas de nivel cero y no por lineas: las restricciones ocupan varias
            // lineas ("CONSTRAINT ... / REFERENCES ..."), y partir por salto de linea tomaria
            // REFERENCES y CHECK como si fueran nombres de columna.
            return PartirPorComas(match.Groups[1].Value)
                .Where(d => !d.StartsWith("CONSTRAINT", StringComparison.Ordinal))
                .Select(d => d.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
                .ToList();
        }

        private static IEnumerable<string> ColumnasAgregadasAPedido() =>
            Regex.Matches(SinComentarios(Script()), @"ADD COLUMN (\w+)")
                .Select(m => m.Groups[1].Value)
                .ToList();

        // Los scripts de este proyecto explican en comentarios lo que hacen, y esos comentarios
        // nombran justo lo que estas pruebas buscan ("ADD COLUMN NOT NULL sin DEFAULT falla",
        // "RAISE EXCEPTION sin ERRCODE deja P0001"). Sin sacarlos, la prueba lee la explicacion
        // en vez del codigo.
        private static string SinComentarios(string sql) =>
            string.Join("\n", sql.Split('\n').Select(l =>
            {
                var i = l.IndexOf("--", StringComparison.Ordinal);
                return i >= 0 ? l[..i] : l;
            }));

        private static IEnumerable<string> PartirPorComas(string cuerpo)
        {
            var partes = new List<string>();
            var actual = new System.Text.StringBuilder();
            var profundidad = 0;

            foreach (var c in cuerpo)
            {
                switch (c)
                {
                    case '(':
                        profundidad++;
                        break;
                    case ')':
                        profundidad--;
                        break;
                    case ',' when profundidad == 0:
                        partes.Add(Normalizar(actual.ToString()));
                        actual.Clear();
                        continue;
                }

                actual.Append(c);
            }

            partes.Add(Normalizar(actual.ToString()));
            return partes.Where(p => p.Length > 0);

            // Las definiciones vienen alineadas en columnas y algunas ocupan varias lineas:
            // todo el espacio en blanco se colapsa para poder tomar el primer token.
            static string Normalizar(string parte) => Regex.Replace(parte, @"\s+", " ").Trim();
        }

        private static string Script(string nombre = "010_turno_caja.sql") =>
            File.ReadAllText(Path.Combine(RaizDelRepositorio(), "sql", nombre));

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
