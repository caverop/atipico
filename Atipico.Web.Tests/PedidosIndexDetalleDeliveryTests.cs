using Atipico.Application.Models;
using Atipico.Domain.Entities;
using Atipico.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PedidosIndex = Atipico.Web.Components.Pages.Pedidos.Index;

namespace Atipico.Web.Tests;

/// <summary>
/// SCRUM-13, ampliación §8.2 de specs/rol-delivery.md — CASO 11 del plan §10.1.
///
/// Complemento del caso 10: allí se fija que una grilla que NO declara <c>DetalleRoles</c>
/// queda como estaba; acá se exige lo contrario para la única que sí lo va a declarar. Con rol
/// Delivery la fila tiene que llevar a la ficha (que §8.1 le abre) y el botón tiene que decir
/// "Ver", no "Editar": un botón que dice "Editar" y abre una pantalla de solo lectura es
/// exactamente la mentira de interfaz que el resto del código evita.
///
/// La contracara —"+ Nuevo" y el resto de la escritura siguen fuera para Delivery— se afirma en
/// el mismo test: es lo que separa "abrir" de "escribir", y es el error más fácil de cometer al
/// implementarlo (meter Delivery en WriteRoles).
///
/// Mismo patrón bUnit que PedidosIndexDeliveryTests.cs: ITurnoApiClient mockeado, la grilla ya
/// llega filtrada por la API (eso lo cubre Atipico.Api.Tests/PedidosControllerDeliveryTests.cs).
/// </summary>
public class PedidosIndexDetalleDeliveryTests : BunitContext
{
    private const long IdPedido = 41;

    private static Pedido PedidoServidoDelivery() => new()
    {
        Id = IdPedido,
        NumeroTurno = 3,
        Comensal = "Ana",
        Estado = Domain.Enums.EstadoPedido.Servido,
        Tipo = Domain.Enums.TipoPedido.Delivery,
        CreadoEn = DateTimeOffset.UtcNow,
        IdMesero = 1,
    };

    private void PrepararDependencias(string rol)
    {
        var turno = new TurnoDto(1, "Turno único", DateTimeOffset.UtcNow, null, "Cajero", 1);
        var grilla = new GrillaPedidosDto(
            turno, [PedidoServidoDelivery()], new Dictionary<long, IReadOnlyList<int>>());

        var turnoApi = new Mock<ITurnoApiClient>();
        turnoApi.Setup(a => a.GetGrillaAbiertaAsync()).ReturnsAsync(grilla);
        turnoApi.Setup(a => a.GetHistoricoAsync()).ReturnsAsync([]);

        var usuarioApi = new Mock<IEntityApiClient<Usuario>>();
        usuarioApi.Setup(a => a.GetAllAsync()).ReturnsAsync([]);

        Services.AddSingleton(turnoApi.Object);
        Services.AddSingleton(usuarioApi.Object);
        Services.AddSingleton<EstadoOperaciones>();

        var auth = this.AddAuthorization();
        auth.SetAuthorized($"{rol.ToLowerInvariant()}-de-prueba");
        auth.SetRoles(rol);
    }

    [Fact]
    public void Grilla_ConRolDelivery_LaFilaLlevaALaFichaYElBotonDice_Ver()
    {
        PrepararDependencias("Delivery");

        var cut = Render<PedidosIndex>();
        cut.WaitForState(() => cut.FindAll("table.table").Count == 1);

        // Ancla: la fila del pedido está en la tabla (si no, las aserciones sobre el enlace
        // podrían pasar por una grilla vacía).
        Assert.Single(cut.FindAll("table.table tbody tr"));

        var enlace = cut.Find($"table.table tbody tr a[href='/pedidos/{IdPedido}']");
        Assert.Equal("Ver", enlace.TextContent.Trim());

        // La tarjeta móvil de la misma fila también abre la ficha (§8.2: los tres lugares se
        // mueven juntos).
        Assert.Equal(
            $"/pedidos/{IdPedido}",
            cut.Find(".entity-cards .entity-card-link").GetAttribute("href"));

        // Abrir no es escribir: "+ Nuevo" se queda en WriteRoles, que no incluye a Delivery.
        Assert.DoesNotContain(cut.FindAll("a"), a => a.TextContent.Trim() == "+ Nuevo");
    }

    [Fact]
    public void Grilla_ConRolAdmin_ElBotonSigueDiciendo_Editar()
    {
        PrepararDependencias("Admin");

        var cut = Render<PedidosIndex>();
        cut.WaitForState(() => cut.FindAll("table.table").Count == 1);

        Assert.Single(cut.FindAll("table.table tbody tr"));

        var enlace = cut.Find($"table.table tbody tr a[href='/pedidos/{IdPedido}']");
        Assert.Equal("Editar", enlace.TextContent.Trim());

        Assert.Contains(cut.FindAll("a"), a => a.TextContent.Trim() == "+ Nuevo");
    }
}
