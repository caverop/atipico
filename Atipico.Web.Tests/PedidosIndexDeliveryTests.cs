using Atipico.Application.Models;
using Atipico.Domain.Entities;
using Atipico.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PedidosIndex = Atipico.Web.Components.Pages.Pedidos.Index;

namespace Atipico.Web.Tests;

/// <summary>
/// SCRUM-13 (specs/rol-delivery.md §6). La lista de pedidos ya llega pre-filtrada por la API
/// para el rol Delivery (eso lo cubre Atipico.Api.Tests/PedidosControllerDeliveryTests.cs); lo
/// que falta ajustar en la página es el mismo trato que ya recibe Mesero para el ALCANCE del
/// turno — no ofrecerle un control de historial que la API igual le va a rechazar con
/// <c>RolesQueAmplian</c> (Forbid). Hoy (spec, antes de implementar) ambos puntos siguen
/// mirando únicamente <c>Roles="Mesero"</c>:
///
///   - El selector "Ver un turno anterior" / "Turno" del panel de filtros
///     (<c>&lt;AuthorizeView Roles="Mesero"&gt;</c>, con contenido solo en
///     <c>&lt;NotAuthorized&gt;</c>): para cualquier rol que NO sea Mesero —incluido
///     Delivery— hoy se MUESTRA, que es exactamente el control que el ticket dice que Delivery
///     no debería ver (§6).
///   - Los filtros de Mesero/Creado, mismo mecanismo.
///
/// Se prueba con bUnit igual que PedidosIndexMesaTests.cs/PedidosIndexOrdenNumeroTests.cs: el
/// backend (ITurnoApiClient) se mockea, y lo que se verifica es lo que la página decide
/// mostrar/ocultar y a qué llama, no ArmarGrillaAsync (ya cubierto en Api.Tests).
///
/// No hay hoy un test equivalente para Mesero en este archivo ni en otro: se verificó antes de
/// escribir este (grep sobre Atipico.Web.Tests) y no existe ninguno que ejercite estas dos
/// AuthorizeView ni el "if" de OnInitializedAsync que decide si se llama a
/// ITurnoApiClient.GetHistoricoAsync(). Este archivo cubre el caso Delivery pedido por el spec;
/// no se agrega un test separado para Mesero por no ser parte del alcance de este ticket.
/// </summary>
public class PedidosIndexDeliveryTests : BunitContext
{
    private static Pedido NuevoPedido(long id, int numeroTurno) => new()
    {
        Id = id,
        NumeroTurno = numeroTurno,
        Comensal = $"Comensal {numeroTurno}",
        Estado = Domain.Enums.EstadoPedido.Servido,
        Tipo = Domain.Enums.TipoPedido.Delivery,
        CreadoEn = DateTimeOffset.UtcNow,
        IdMesero = 1,
    };

    private Mock<ITurnoApiClient> PrepararDependencias(GrillaPedidosDto grilla, string rol)
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
        auth.SetAuthorized("delivery-de-prueba");
        auth.SetRoles(rol);

        return turnoApi;
    }

    // Abre el panel "Filtros" (colapsado por defecto) para poder inspeccionar sus campos.
    private static void AbrirPanelDeFiltros<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        cut.Find("button.filtros-toggle").Click();
        cut.WaitForState(() => cut.FindAll(".filtros-panel").Count == 1);
    }

    [Fact]
    public void Index_ConRolDelivery_NoMuestraElSelectorDeTurnoNiLosFiltrosDeMeseroYCreado()
    {
        var turno = new TurnoDto(1, "Turno único", DateTimeOffset.UtcNow, null, "Cajero", 1);
        var grilla = new GrillaPedidosDto(turno, [NuevoPedido(1, 1)], new Dictionary<long, IReadOnlyList<int>>());
        PrepararDependencias(grilla, "Delivery");

        var cut = Render<PedidosIndex>();
        cut.WaitForState(() => cut.FindAll("table.table").Count == 1);
        AbrirPanelDeFiltros(cut);

        var etiquetas = cut.FindAll(".filtros-panel label")
            .Select(l => l.TextContent.Trim())
            .ToList();

        // Se quedan: son de alcance del turno, no de administracion/ampliacion (§6.1).
        Assert.Contains("N°", etiquetas);
        Assert.Contains("Tipo", etiquetas);

        // Se ocultan para Delivery, igual que para Mesero (§6): ofrecerlos es mostrar un
        // control que la API va a rechazar con Forbid si Delivery lo usa (RolesQueAmplian).
        Assert.DoesNotContain("Turno", etiquetas);
        Assert.DoesNotContain("Mesero", etiquetas);
        Assert.DoesNotContain("Creado", etiquetas);
    }

    // Complementa el test anterior: no alcanza con esconder el control, tampoco se debe pagar
    // la llamada a GetHistoricoAsync() para un rol que no la va a poder usar (mismo trato que
    // ya recibe Mesero en OnInitializedAsync, spec §6).
    [Fact]
    public void Index_ConRolDelivery_NoPideElHistoricoDeTurnos()
    {
        var turno = new TurnoDto(1, "Turno único", DateTimeOffset.UtcNow, null, "Cajero", 1);
        var grilla = new GrillaPedidosDto(turno, [NuevoPedido(1, 1)], new Dictionary<long, IReadOnlyList<int>>());
        var turnoApi = PrepararDependencias(grilla, "Delivery");

        var cut = Render<PedidosIndex>();
        cut.WaitForState(() => cut.FindAll("table.table").Count == 1);

        turnoApi.Verify(a => a.GetHistoricoAsync(), Times.Never);
    }
}
