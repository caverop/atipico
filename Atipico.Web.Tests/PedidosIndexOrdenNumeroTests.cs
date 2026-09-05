using Atipico.Application.Models;
using Atipico.Domain.Entities;
using Atipico.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PedidosIndex = Atipico.Web.Components.Pages.Pedidos.Index;

namespace Atipico.Web.Tests;

/// <summary>
/// SCRUM-18 (specs/orden-por-defecto-numero-pedido.md §6): la grilla de Pedidos/Index.razor
/// abre ordenada por N° (<c>p.NumeroTurno</c>) ascendente, no por Creado descendente como
/// antes. El mecanismo de orden en sí (el switch de _pedidosOrdenados, EntityTable) no cambia
/// (spec §1) — lo único que cambia son los valores iniciales de _ordenColumna/_ordenDesc y la
/// condición de Ordenar(string columna) que decide la dirección al elegir "N°" desde otra
/// columna (spec §2.2).
///
/// Mismo patrón de mocks que PedidosIndexMesaTests.cs (SCRUM-16): ITurnoApiClient mockeado,
/// rol Cocinero para no agregar columnas de EntityTable, y GrillaPedidosDto con
/// MesasPorPedido vacío porque estos tests no ejercitan la columna Mesa.
///
/// Todos los pedidos se construyen con NumeroTurno que NO coincide con el orden de CreadoEn
/// (spec §6.1): así una aserción de orden ascendente por N° no puede pasar "por accidente"
/// con el criterio viejo (Creado descendente).
/// </summary>
public class PedidosIndexOrdenNumeroTests : BunitContext
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

    // El orden de CreadoEn (por DÍA, que es como lo compara _pedidosOrdenados) queda a
    // propósito DESACOPLADO del orden de NumeroTurno: NumeroTurno 3, 1, 2 se crean hoy, ayer y
    // anteayer respectivamente. Con el criterio VIEJO (Creado descendente) el resultado sale
    // 003, 001, 002 — distinto de la ascendente esperada por N° (001, 002, 003) — así la
    // aserción no puede pasar "por accidente" contra el comportamiento actual (spec §6.1).
    // Al ser días distintos no hay empate en Creado, así que el desempate por Comensal/Estado
    // tampoco entra en juego bajo el criterio viejo.
    private static GrillaPedidosDto GrillaConNumerosDesordenados()
    {
        var turno = new TurnoDto(1, "Turno único", DateTimeOffset.UtcNow, null, "Cajero", 3);
        var hoy = DateTimeOffset.UtcNow;

        var creadoHoy = NuevoPedido(30, 3, hoy);
        var creadoAyer = NuevoPedido(31, 1, hoy.AddDays(-1));
        var creadoAnteayer = NuevoPedido(32, 2, hoy.AddDays(-2));

        return new GrillaPedidosDto(
            turno,
            [creadoHoy, creadoAyer, creadoAnteayer],
            new Dictionary<long, IReadOnlyList<int>>());
    }

    private static List<string> NumerosEnPantalla<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll("table.table tbody tr")
            .Select(fila => fila.QuerySelectorAll("td")[0].TextContent.Trim())
            .ToList();

    [Fact]
    public void Index_SinHacerClic_OrdenaPorNumeroAscendente()
    {
        PrepararDependencias(GrillaConNumerosDesordenados());

        var cut = Render<PedidosIndex>();
        cut.WaitForState(() => cut.FindAll("table.table").Count == 1);

        cut.WaitForAssertion(() =>
            Assert.Equal(["001", "002", "003"], NumerosEnPantalla(cut)));
    }

    [Fact]
    public void Index_VolverANumeroDesdeOtraColumna_ArrancaAscendente()
    {
        PrepararDependencias(GrillaConNumerosDesordenados());

        var cut = Render<PedidosIndex>();
        cut.WaitForState(() => cut.FindAll("table.table").Count == 1);

        var botonEstado = cut.FindAll("table.table thead th")
            .Single(th => th.TextContent.Trim() == "Estado")
            .QuerySelector("button");
        Assert.NotNull(botonEstado);
        botonEstado!.Click();

        var botonNumero = cut.FindAll("table.table thead th")
            .Single(th => th.TextContent.Trim() == "N°")
            .QuerySelector("button");
        Assert.NotNull(botonNumero);
        botonNumero!.Click();

        cut.WaitForAssertion(() =>
            Assert.Equal(["001", "002", "003"], NumerosEnPantalla(cut)));
    }

    [Fact]
    public void Index_NumeroYaActivo_UnClicInvierteADescendente()
    {
        PrepararDependencias(GrillaConNumerosDesordenados());

        var cut = Render<PedidosIndex>();
        cut.WaitForState(() => cut.FindAll("table.table").Count == 1);

        // Estado inicial: N° ya activo y ascendente, sin haber hecho clic todavía.
        cut.WaitForAssertion(() =>
            Assert.Equal(["001", "002", "003"], NumerosEnPantalla(cut)));

        var botonNumero = cut.FindAll("table.table thead th")
            .Single(th => th.TextContent.Trim() == "N°")
            .QuerySelector("button");
        Assert.NotNull(botonNumero);
        botonNumero!.Click();

        cut.WaitForAssertion(() =>
            Assert.Equal(["003", "002", "001"], NumerosEnPantalla(cut)));
    }
}
