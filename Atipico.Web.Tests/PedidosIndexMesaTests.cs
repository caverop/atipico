using Atipico.Application.Models;
using Atipico.Domain.Entities;
using Atipico.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PedidosIndex = Atipico.Web.Components.Pages.Pedidos.Index;

namespace Atipico.Web.Tests;

/// <summary>
/// Spec §6.3 de specs/numero-mesa-grilla-pedidos.md: columna "Mesa" y caso de orden "Mesa" en
/// Pedidos/Index.razor. No hay un patrón de test de páginas Razor con dependencias/auth en el
/// repo más allá de LoginTests.cs (una página sin @inject y sin [Authorize]) — este archivo
/// extiende ese patrón para una página con servicios inyectados y roles, usando bUnit contra
/// un ITurnoApiClient mockeado (el límite del test es la respuesta HTTP ya deserializada, no
/// la página en sí).
///
/// El backend (ITurnoApiClient) se mockea: estos tests no ejercitan ArmarGrillaAsync ni
/// MesasPorPedidoAsync (eso ya lo cubre PedidosControllerTurnoTests). Lo que se verifica acá es
/// que Pedidos/Index.razor, dado un GrillaPedidosDto.MesasPorPedido ya resuelto, lo muestra y
/// lo usa para ordenar.
///
/// Igual que Atipico.Api.Tests/PedidosControllerTurnoTests.cs, este archivo hoy NO COMPILA:
/// GrillaPedidosDto todavía tiene el constructor de dos parámetros (Turno, Pedidos). El tercer
/// argumento posicional (MesasPorPedido) es justamente lo que falta implementar (spec §3). Es
/// el mismo rojo que el de la API, no un rojo nuevo.
/// </summary>
public class PedidosIndexMesaTests : BunitContext
{
    private static Pedido NuevoPedido(long id, int numeroTurno, DateTimeOffset creadoEn) => new()
    {
        Id = id,
        NumeroTurno = numeroTurno,
        Comensal = $"Comensal {numeroTurno}",
        Estado = Domain.Enums.EstadoPedido.Abierto,
        Tipo = Domain.Enums.TipoPedido.EnSalon,
        CreadoEn = creadoEn,
        IdMesero = 1,
    };

    // Registra los servicios que Pedidos/Index.razor inyecta y deja el componente en un rol
    // que no es Admin ni Mesero: así ninguna de las dos AuthorizeView de EntityTable agrega
    // una columna de acciones extra a la tabla, y los índices de columna quedan estables.
    private void PrepararDependencias(GrillaPedidosDto grilla)
    {
        var turnoApi = new Mock<ITurnoApiClient>();
        turnoApi.Setup(a => a.GetGrillaAbiertaAsync()).ReturnsAsync(grilla);
        turnoApi.Setup(a => a.GetHistoricoAsync()).ReturnsAsync([]);

        var usuarioApi = new Mock<IEntityApiClient<Usuario>>();
        usuarioApi.Setup(a => a.GetAllAsync()).ReturnsAsync([]);

        Services.AddSingleton(turnoApi.Object);
        Services.AddSingleton(usuarioApi.Object);
        Services.AddSingleton<EstadoOperaciones>();

        var auth = this.AddAuthorization();
        auth.SetAuthorized("cocinero-de-prueba");
        auth.SetRoles("Cocinero");
    }

    [Fact]
    public void Index_ColumnaMesa_FormateaSegunCantidadDeMesas()
    {
        var turno = new TurnoDto(1, "Turno único", DateTimeOffset.UtcNow, null, "Cajero", 3);

        var sinMesa = NuevoPedido(10, 1, DateTimeOffset.UtcNow);
        var unaMesa = NuevoPedido(11, 2, DateTimeOffset.UtcNow);
        var dosMesas = NuevoPedido(12, 3, DateTimeOffset.UtcNow);

        // Pedido 10 queda deliberadamente SIN entrada (misma decisión documentada en
        // PedidosControllerTurnoTests para el caso "pedido sin mesa").
        var mesasPorPedido = new Dictionary<long, IReadOnlyList<int>>
        {
            [unaMesa.Id] = new List<int> { 5 },
            [dosMesas.Id] = new List<int> { 3, 8 },
        };

        var grilla = new GrillaPedidosDto(turno, [sinMesa, unaMesa, dosMesas], mesasPorPedido);
        PrepararDependencias(grilla);

        var cut = Render<PedidosIndex>();
        cut.WaitForState(() => cut.FindAll("table.table").Count == 1);

        var encabezados = cut.FindAll("table.table thead th")
            .Select(th => th.TextContent.Trim())
            .ToList();

        // CA §4.1: la columna se llama "Mesa" y va entre Comensal y Estado.
        Assert.Equal(["N°", "Comensal", "Mesa", "Estado", "Tipo", "Creado"], encabezados);

        var indiceMesa = encabezados.IndexOf("Mesa");
        var filas = cut.FindAll("table.table tbody tr");
        var celdaMesaPorNumero = filas.ToDictionary(
            fila => fila.QuerySelectorAll("td")[0].TextContent.Trim(),
            fila => fila.QuerySelectorAll("td")[indiceMesa].TextContent.Trim());

        Assert.Equal("—", celdaMesaPorNumero["001"]);
        Assert.Equal("5", celdaMesaPorNumero["002"]);
        Assert.Equal("3, 8", celdaMesaPorNumero["003"]);
    }

    [Fact]
    public void Index_OrdenarPorMesa_UsaLaMesaMenorDeCadaPedidoYSinMesaOrdenaComoCero()
    {
        var turno = new TurnoDto(1, "Turno único", DateTimeOffset.UtcNow, null, "Cajero", 3);
        var ahora = DateTimeOffset.UtcNow;

        // NumeroTurno 1 = sin mesa (ordena como 0), NumeroTurno 2 = mesa mínima 5,
        // NumeroTurno 3 = mesas 3 y 8 (mesa mínima 3). Ascendente por mesa mínima esperado:
        // 001 (0), 003 (3), 002 (5).
        var sinMesa = NuevoPedido(20, 1, ahora);
        var mesaCinco = NuevoPedido(21, 2, ahora);
        var mesasTresYOcho = NuevoPedido(22, 3, ahora);

        var mesasPorPedido = new Dictionary<long, IReadOnlyList<int>>
        {
            [mesaCinco.Id] = new List<int> { 5 },
            [mesasTresYOcho.Id] = new List<int> { 3, 8 },
        };

        var grilla = new GrillaPedidosDto(turno, [sinMesa, mesaCinco, mesasTresYOcho], mesasPorPedido);
        PrepararDependencias(grilla);

        var cut = Render<PedidosIndex>();
        cut.WaitForState(() => cut.FindAll("table.table").Count == 1);

        var botonMesa = cut.FindAll("table.table thead th")
            .Single(th => th.TextContent.Trim() == "Mesa")
            .QuerySelector("button");
        Assert.NotNull(botonMesa);
        botonMesa!.Click();

        // WaitForAssertion reintenta hasta que la aserción deje de fallar (o el timeout):
        // el clic dispara Ordenar (síncrono) pero el re-render pasa por el pipeline normal
        // de Blazor, así que no está garantizado que ya haya ocurrido en la misma línea.
        cut.WaitForAssertion(() =>
        {
            var numerosEnOrden = cut.FindAll("table.table tbody tr")
                .Select(fila => fila.QuerySelectorAll("td")[0].TextContent.Trim())
                .ToList();

            Assert.Equal(["001", "003", "002"], numerosEnOrden);
        });
    }
}
