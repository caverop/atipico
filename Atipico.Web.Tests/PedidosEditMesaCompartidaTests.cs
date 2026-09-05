using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PedidoEdit = Atipico.Web.Components.Pages.Pedidos.Edit;

namespace Atipico.Web.Tests;

/// <summary>
/// Spec §8.4 de specs/mesa-compartida-por-turno.md: con una mesa <c>Ocupada</c> en
/// <c>_mesas</c> y ninguna <c>Libre</c>, la opción del &lt;select&gt; de "Mesas del pedido"
/// deja de estar <c>disabled</c> (§5) y "Agregar mesa" la asocia llamando a
/// <c>PedidoMesaApi.CreateAsync</c>. No se ejercita el trigger real (vive en la base, ver
/// specs/mesa-compartida-por-turno.md §8.2 y §8.3) — esto solo prueba el <see cref="Atipico.Web.Components.Pages.Pedidos.Edit"/>
/// de Blazor, con todos los <c>IEntityApiClient&lt;T&gt;</c> que inyecta mockeados.
/// </summary>
public class PedidosEditMesaCompartidaTests : BunitContext
{
    private readonly Mesa _mesaOcupada = new() { Id = 5, Numero = 12, Capacidad = 8, Estado = EstadoMesa.Ocupada };

    private static Pedido PedidoAbiertoEnSalon(long id) => new()
    {
        Id = id,
        Comensal = "Mesa grande",
        Estado = EstadoPedido.Abierto,
        Tipo = TipoPedido.EnSalon,
        NumeroTurno = 7,
        IdMesero = 1,
    };

    // Registra los servicios que Pedidos/Edit.razor inyecta. El rol Mesero alcanza para pasar
    // el [Authorize(Roles = "Admin,Mesero")] de la página.
    private Mock<IEntityApiClient<PedidoMesa>> PrepararDependencias(Pedido pedido)
    {
        var pedidoApi = new Mock<IEntityApiClient<Pedido>>();
        pedidoApi.Setup(a => a.GetByIdAsync(pedido.Id)).ReturnsAsync(pedido);

        var usuarioApi = new Mock<IEntityApiClient<Usuario>>();
        usuarioApi.Setup(a => a.GetAllAsync()).ReturnsAsync([]);

        var platoApi = new Mock<IEntityApiClient<Plato>>();
        platoApi.Setup(a => a.GetAllAsync()).ReturnsAsync([]);

        var pedidoPlatoApi = new Mock<IEntityApiClient<PedidoPlato>>();
        pedidoPlatoApi.Setup(a => a.GetAllAsync()).ReturnsAsync([]);

        var mesaApi = new Mock<IEntityApiClient<Mesa>>();
        mesaApi.Setup(a => a.GetAllAsync()).ReturnsAsync([_mesaOcupada]);

        var pedidoMesaApi = new Mock<IEntityApiClient<PedidoMesa>>();
        pedidoMesaApi.Setup(a => a.GetAllAsync()).ReturnsAsync([]);
        pedidoMesaApi.Setup(a => a.CreateAsync(It.IsAny<PedidoMesa>()))
            .ReturnsAsync((PedidoMesa pm) => pm);

        var detalleCuentaApi = new Mock<IEntityApiClient<DetalleCuenta>>();
        detalleCuentaApi.Setup(a => a.GetAllAsync()).ReturnsAsync([]);

        var cuentaApi = new Mock<IEntityApiClient<Cuenta>>();
        cuentaApi.Setup(a => a.GetAllAsync()).ReturnsAsync([]);

        Services.AddSingleton(pedidoApi.Object);
        Services.AddSingleton(usuarioApi.Object);
        Services.AddSingleton(platoApi.Object);
        Services.AddSingleton(pedidoPlatoApi.Object);
        Services.AddSingleton(mesaApi.Object);
        Services.AddSingleton(pedidoMesaApi.Object);
        Services.AddSingleton(detalleCuentaApi.Object);
        Services.AddSingleton(cuentaApi.Object);
        Services.AddSingleton(new Mock<IComprobanteApiClient>().Object);
        Services.AddSingleton(new Mock<IResolvedorEnlaceUbicacion>().Object);
        Services.AddSingleton(new Mock<IHttpClientFactory>().Object);
        Services.AddSingleton<EstadoOperaciones>();

        var auth = this.AddAuthorization();
        auth.SetAuthorized("mesero-de-prueba");
        auth.SetRoles("Mesero");

        return pedidoMesaApi;
    }

    [Fact]
    public void Edit_ConMesaOcupadaYSinLibres_LaOpcionEstaHabilitadaYSeAsociaAlAgregar()
    {
        var pedido = PedidoAbiertoEnSalon(100);
        var pedidoMesaApi = PrepararDependencias(pedido);

        var cut = Render<PedidoEdit>(parameters => parameters.Add(p => p.Id, pedido.Id));

        cut.WaitForState(() => cut.FindAll("select.form-select").Count > 0);

        // §5: la opción de una mesa Ocupada deja de estar disabled y sigue informando que
        // está compartida. Hoy (antes de SCRUM-17) el <select> ni siquiera se dibuja cuando no
        // queda ninguna mesa Libre (_mesasLibres.Count == 0 esconde todo el bloque detrás de
        // "Todas las mesas están ocupadas..."), así que este Find falla por ausencia total del
        // elemento, no solo por el atributo disabled.
        var opcion = cut.Find($"option[value='{_mesaOcupada.Id}']");
        Assert.False(opcion.HasAttribute("disabled"));
        Assert.Contains("ocupada por otro pedido", opcion.TextContent);

        var boton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Agregar mesa");
        boton.Click();

        // §5: _mesaPorDefecto ya no se resigna a 0 sin mesas libres, así que "Agregar mesa"
        // asocia la Ocupada sin que haga falta simular una selección manual en el <select>.
        cut.WaitForAssertion(() =>
            pedidoMesaApi.Verify(
                a => a.CreateAsync(It.Is<PedidoMesa>(pm => pm.IdMesa == _mesaOcupada.Id && pm.IdPedido == pedido.Id)),
                Times.Once()));
    }
}
