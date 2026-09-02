using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Infraestructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Atipico.Infraestructure.Tests
{
    // Generaliza a los 8 enums del dominio lo que ModeloTipoPedidoTest hace para TipoPedido.
    //
    // CLAUDE.md lo dice explicito: los CHECK de sql/ duplican A MANO los enums de C# y "no hay
    // una unica fuente de verdad; las dos se cambian juntas a mano". Una divergencia no rompe
    // el build ni el arranque: revienta con el primer INSERT, en produccion. Estas pruebas
    // convierten esa disciplina manual en algo que falla en CI.
    //
    // Se compara contra el conversor REAL del modelo de EF (no contra una reimplementacion de
    // UpperSnakeCaseEnumConverter), asi que tambien detecta que un mapeo se haya configurado
    // distinto de lo esperado.
    public class ModeloEnumsCheckTest
    {
        private static AppDbContext CrearContexto()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=no_se_conecta")
                .Options;

            return new AppDbContext(options);
        }

        // entidad, propiedad, constraint. TipoPedido queda afuera: ya lo cubre
        // ModeloTipoPedidoTest. MetodoPago tambien, por el drift documentado (ver mas abajo).
        public static TheoryData<Type, string, string> EnumsConCheckEstricto() => new()
        {
            { typeof(Usuario),     nameof(Usuario.Rol),        "ck_usuario_rol" },
            { typeof(Plato),       nameof(Plato.Estado),       "ck_plato_estado" },
            { typeof(Mesa),        nameof(Mesa.Estado),        "ck_mesa_estado" },
            { typeof(Pedido),      nameof(Pedido.Estado),      "ck_pedido_estado" },
            { typeof(PedidoPlato), nameof(PedidoPlato.Estado), "ck_pedido_plato_estado" },
            { typeof(Cuenta),      nameof(Cuenta.Estado),      "ck_cuenta_estado" },
        };

        [Theory]
        [MemberData(nameof(EnumsConCheckEstricto))]
        public void ElEnumYElCheckTienenExactamenteLosMismosLiterales(
            Type entidad, string propiedad, string constraint)
        {
            var conversor = ConversorDe(entidad, propiedad);
            var tipoEnum = TipoDelEnum(entidad, propiedad);

            var serializados = Enum.GetValues(tipoEnum)
                .Cast<object>()
                .Select(v => (string)conversor.ConvertToProvider(v)!)
                .OrderBy(v => v, StringComparer.Ordinal);

            Assert.Equal(
                LiteralesDelCheck(constraint).OrderBy(v => v, StringComparer.Ordinal),
                serializados);
        }

        [Theory]
        [MemberData(nameof(EnumsConCheckEstricto))]
        public void CadaLiteralDelCheckVuelveASuValorDelEnum(
            Type entidad, string propiedad, string constraint)
        {
            var conversor = ConversorDe(entidad, propiedad);

            foreach (var literal in LiteralesDelCheck(constraint))
            {
                var valor = conversor.ConvertFromProvider(literal);

                Assert.NotNull(valor);
                Assert.Equal(literal, conversor.ConvertToProvider(valor)!.ToString());
            }
        }

        // MetodoPago es el unico caso de DRIFT DELIBERADO: la base permite
        // EFECTIVO/TARJETA/TRANSFERENCIA/QR y el enum de C# quedo reducido a Efectivo y Qr.
        // La decision registrada fue angostar el enum sin migrar la base, porque un CHECK que
        // es superconjunto estricto de lo que C# usa es holgura inofensiva. Por eso aca se
        // exige subconjunto, no igualdad: lo que NO puede pasar es que C# emita un literal que
        // la base rechace.
        [Fact]
        public void MetodoPagoEsSubconjuntoDeCkCuentaMetodo()
        {
            var conversor = ConversorDe(typeof(Cuenta), nameof(Cuenta.MetodoPago));
            var permitidos = LiteralesDelCheck("ck_cuenta_metodo").ToHashSet(StringComparer.Ordinal);

            foreach (var valor in Enum.GetValues<MetodoPago>())
            {
                var literal = (string)conversor.ConvertToProvider(valor)!;

                Assert.True(
                    permitidos.Contains(literal),
                    $"MetodoPago.{valor} se serializa como '{literal}', que ck_cuenta_metodo no permite. " +
                    $"Permitidos: {string.Join(", ", permitidos)}.");
            }
        }

        private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter
            ConversorDe(Type entidad, string propiedad)
        {
            using var contexto = CrearContexto();
            var prop = contexto.Model.FindEntityType(entidad)?.FindProperty(propiedad);

            Assert.True(prop is not null, $"{entidad.Name}.{propiedad} no esta mapeada en el modelo.");

            var conversor = prop!.GetValueConverter();
            Assert.True(conversor is not null, $"{entidad.Name}.{propiedad} no tiene value converter.");

            return conversor!;
        }

        private static Type TipoDelEnum(Type entidad, string propiedad)
        {
            var tipo = entidad.GetProperty(propiedad)!.PropertyType;
            return Nullable.GetUnderlyingType(tipo) ?? tipo;
        }

        // PRECEDENCIA, en el orden de autoridad que documenta el propio repo:
        //
        //   1. La migracion numerada mas alta que la defina — el historial canonico.
        //   2. schema_completo.sql — snapshot verificado con pg_dump contra la base real. Su
        //      cabecera dice explicitamente que resuelve a favor de lo desplegado el desacuerdo
        //      conocido de ck_cuenta_metodo.
        //   3. script_inicial.sql — el mas viejo, y "no se edita una vez aplicado", asi que
        //      puede quedar desactualizado por diseno (documenta YAPE/PLIN, la base tiene QR).
        //
        // No alcanza con ordenar alfabeticamente: "schema_completo" < "script_inicial", asi que
        // el orden simple le daba la autoridad justo al archivo que se sabe desactualizado.
        //
        // CAVEAT: schema_completo.sql se genero el 2026-08-18 y solo cubre hasta la migracion
        // 005. Para constraints de la 006 en adelante la regla 1 las encuentra igual; pero si
        // alguna vez se MODIFICA una constraint vieja sin regenerar el snapshot, el valor leido
        // va a ser el del snapshot. Regenerar schema_completo.sql en ese caso.
        private static IEnumerable<string> LiteralesDelCheck(string constraint)
        {
            var sqlDir = Path.Combine(RaizDelRepositorio(), "sql");

            var numeradas = Directory
                .GetFiles(sqlDir, "*.sql")
                .Where(f => char.IsDigit(Path.GetFileName(f)[0]))
                .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal);

            var archivos = numeradas
                .Append(Path.Combine(sqlDir, "schema_completo.sql"))
                .Append(Path.Combine(sqlDir, "script_inicial.sql"))
                .Where(File.Exists);

            // Dos formatos, porque las dos fuentes escriben distinto:
            //   - Las migraciones a mano usan  IN ('A','B')          (tolera multilinea, como
            //     ck_cuenta_metodo: "... IS NULL OR ... IN (...)").
            //   - schema_completo.sql es salida literal de pg_dump, y PostgreSQL almacena las
            //     listas como  = ANY (ARRAY['A'::character varying, ...])
            // Los literales se extraen igual en ambos casos: lo que va entre comillas simples.
            // Se ancla en la palabra CONSTRAINT, no en el nombre suelto. Eso evita dos falsos
            // positivos reales: (1) los archivos MENCIONAN constraints en comentarios de
            // mantenimiento — 004_plato_estado_habilitado.sql nombra ck_pedido_plato_estado en
            // prosa y a continuacion define ck_plato_estado, asi que buscar el nombre suelto
            // devolvia la lista equivocada; (2) "ck_plato_estado" es substring de
            // "ck_pedido_plato_estado". El \b cierra el caso de un sufijo tipo _2.
            var patron = new Regex(
                @"CONSTRAINT\s+" + Regex.Escape(constraint) + @"\b\s+CHECK" +
                @"[\s\S]{0,300}?(?:\bIN\s*\(|=\s*ANY\s*\(+\s*ARRAY\s*\[)([^)\]]*)",
                RegexOptions.IgnoreCase);

            // Gana la PRIMERA coincidencia: la lista ya viene en orden de autoridad.
            string? valores = null;

            foreach (var archivo in archivos)
            {
                var match = patron.Match(File.ReadAllText(archivo));
                if (match.Success)
                {
                    valores = match.Groups[1].Value;
                    break;
                }
            }

            Assert.True(valores is not null, $"No se encontro la restriccion {constraint} en ningun sql/*.sql.");

            // Se extrae lo entrecomillado en vez de partir por comas: en la forma de pg_dump
            // cada elemento arrastra su cast ('EFECTIVO'::character varying).
            return Regex.Matches(valores!, @"'([^']*)'").Select(m => m.Groups[1].Value);
        }

        // El binario vive varios niveles debajo de la raiz y la profundidad cambia segun la
        // configuracion: se sube hasta encontrar sql/ en vez de contar carpetas.
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
