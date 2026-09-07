using Atipico.Application.Common;
using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Web.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PedidoEdit = Atipico.Web.Components.Pages.Pedidos.Edit;

namespace Atipico.Web.Tests;

/// <summary>
/// SCRUM-13, ampliación §8 de specs/rol-delivery.md: Delivery entra a la ficha del pedido a
/// VER (dirección, mapa, platos) y no a editar. Cubre los casos 7, 8 y 9 del plan §10.1.
///
/// Por qué esto NO se renderiza con <c>Render&lt;PedidoEdit&gt;</c> como
/// PedidosEditMesaCompartidaTests.cs: bUnit no evalúa el atributo <c>[Authorize]</c> de un
/// componente al renderizarlo directo — ese chequeo lo hace <c>AuthorizeRouteView</c>, que en
/// la aplicación real vive en Routes.razor. Un test que renderizara la página a secas con rol
/// Delivery pasaría hoy mismo, antes de tocar el <c>[Authorize(Roles = "Admin,Mesero")]</c>, y
/// no probaría nada del caso 7 ("la ficha se abre, no redirige"). Por eso el harness monta
/// <c>AuthorizeRouteView</c> con el mismo <c>RouteData</c> que armaría el Router, y pone en su
/// <c>NotAuthorized</c> un marcador <c>[data-rebote]</c> que representa lo que en producción es
/// <c>RedirectToLogin</c>.
///
/// El resto de las dependencias sale del PrepararDependencias de
/// PedidosEditMesaCompartidaTests.cs (SCRUM-17), adaptado: la ficha inyecta diez servicios y
/// todos se mockean. El límite del test es la respuesta ya deserializada de la API.
/// </summary>
public class PedidosEditDeliveryTests : BunitContext
{
    private const long IdPedido = 100;
    private const long IdPlato = 7;
    private const long IdPedidoPlato = 70;
    private const long IdCuentaQr = 900;
    private const decimal Latitud = -17.783700m;
    private const decimal Longitud = -63.182100m;

    private const string NombrePlato = "Silpancho";

    // Mesa 12 ya asociada al pedido (para que la sección "Mesas del pedido" se dibuje: con
    // Tipo = Delivery, _mostrarMesas solo es true si el pedido ya tiene mesas — spec §8.1) y
    // mesa 3 libre, para que "Agregar mesa" tenga algo que ofrecer.
    private readonly Mesa _mesaDelPedido = new() { Id = 5, Numero = 12, Capacidad = 8, Estado = EstadoMesa.Ocupada };
    private readonly Mesa _mesaLibre = new() { Id = 6, Numero = 3, Capacidad = 4, Estado = EstadoMesa.Libre };

    // El pedido que Delivery SÍ puede abrir: Servido + Delivery (§2.2 garantiza que ningún
    // otro le llega; GetById le devuelve 404).
    private static Pedido PedidoServidoDelivery() => new()
    {
        Id = IdPedido,
        Comensal = "Ana",
        Estado = EstadoPedido.Servido,
        Tipo = TipoPedido.Delivery,
        NumeroTurno = 12,
        IdMesero = 1,
        DireccionEntrega = "Casa verde, media cuadra del surtidor",
        LatitudEntrega = Latitud,
        LongitudEntrega = Longitud,
        CreadoEn = DateTimeOffset.UtcNow,
    };

    private void PrepararDependencias(Pedido pedido, string rol)
    {
        var pedidoApi = new Mock<IEntityApiClient<Pedido>>();
        pedidoApi.Setup(a => a.GetByIdAsync(pedido.Id)).ReturnsAsync(pedido);

        var usuarioApi = new Mock<IEntityApiClient<Usuario>>();
        usuarioApi.Setup(a => a.GetAllAsync()).ReturnsAsync([]);

        var platoApi = new Mock<IEntityApiClient<Plato>>();
        platoApi.Setup(a => a.GetAllAsync()).ReturnsAsync(
        [
            new Plato
            {
                Id = IdPlato,
                Nombre = NombrePlato,
                Precio = 35m,
                Activo = true,
                HabilitadoDesde = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            },
        ]);

        var pedidoPlatoApi = new Mock<IEntityApiClient<PedidoPlato>>();
        pedidoPlatoApi.Setup(a => a.GetAllAsync()).ReturnsAsync(
        [
            new PedidoPlato
            {
                Id = IdPedidoPlato,
                IdPedido = pedido.Id,
                IdPlato = IdPlato,
                Estado = EstadoPedidoPlato.Servido,
            },
        ]);

        var mesaApi = new Mock<IEntityApiClient<Mesa>>();
        mesaApi.Setup(a => a.GetAllAsync()).ReturnsAsync([_mesaDelPedido, _mesaLibre]);

        var pedidoMesaApi = new Mock<IEntityApiClient<PedidoMesa>>();
        pedidoMesaApi.Setup(a => a.GetAllAsync()).ReturnsAsync(
        [
            new PedidoMesa { Id = 50, IdPedido = pedido.Id, IdMesa = _mesaDelPedido.Id },
        ]);

        // Cadena Pedido -> PedidoPlato -> DetalleCuenta -> Cuenta que arma _cuentasQr, para que
        // el ComprobantesPanel (cuarto punto de §8.1) llegue a dibujarse.
        var detalleCuentaApi = new Mock<IEntityApiClient<DetalleCuenta>>();
        detalleCuentaApi.Setup(a => a.GetAllAsync()).ReturnsAsync(
        [
            new DetalleCuenta { Id = 800, IdCuenta = IdCuentaQr, IdPedidoPlato = IdPedidoPlato, PrecioUnitario = 35m },
        ]);

        var cuentaApi = new Mock<IEntityApiClient<Cuenta>>();
        cuentaApi.Setup(a => a.GetAllAsync()).ReturnsAsync(
        [
            new Cuenta
            {
                Id = IdCuentaQr,
                Estado = EstadoCuenta.Pagada,
                MetodoPago = MetodoPago.Qr,
                Monto = 35m,
                IdMesero = 1,
            },
        ]);

        // Un comprobante ya cargado: sin él, "Reemplazar" no se dibuja para NADIE y la
        // aserción de ausencia del caso 8 sobre ese botón sería un verde vacío.
        var comprobanteApi = new Mock<IComprobanteApiClient>();
        comprobanteApi.Setup(a => a.GetPorCuentaAsync(IdCuentaQr)).ReturnsAsync(
        [
            new ComprobantePago
            {
                Id = 1,
                IdCuenta = IdCuentaQr,
                Monto = 35m,
                StorageKey = "comprobantes/1.jpg",
                HashSha256 = new string('a', 64),
                TipoContenido = "image/jpeg",
                Bytes = 1024,
                CreadoEn = DateTimeOffset.UtcNow,
                IdSubidoPor = 1,
            },
        ]);

        Services.AddSingleton(pedidoApi.Object);
        Services.AddSingleton(usuarioApi.Object);
        Services.AddSingleton(platoApi.Object);
        Services.AddSingleton(pedidoPlatoApi.Object);
        Services.AddSingleton(mesaApi.Object);
        Services.AddSingleton(pedidoMesaApi.Object);
        Services.AddSingleton(detalleCuentaApi.Object);
        Services.AddSingleton(cuentaApi.Object);
        Services.AddSingleton(comprobanteApi.Object);
        Services.AddSingleton(new Mock<IResolvedorEnlaceUbicacion>().Object);
        Services.AddSingleton(new Mock<IHttpClientFactory>().Object);
        Services.AddSingleton<EstadoOperaciones>();

        var auth = this.AddAuthorization();
        auth.SetAuthorized($"{rol.ToLowerInvariant()}-de-prueba");
        auth.SetRoles(rol);
    }

    // Lo que hace Routes.razor: el Router encuentra la página, AuthorizeRouteView decide si la
    // dibuja o si manda al NotAuthorized (allí, RedirectToLogin; acá, un marcador).
    private IRenderedComponent<AuthorizeRouteView> RenderizarFicha(long id)
    {
        RenderFragment<AuthenticationState> rebote = _ => builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-rebote", "1");
            builder.AddContent(2, "Rebotado por AuthorizeRouteView");
            builder.CloseElement();
        };

        var cut = Render<AuthorizeRouteView>(parameters => parameters
            .Add(p => p.RouteData, new RouteData(typeof(PedidoEdit), new Dictionary<string, object?> { ["Id"] = id }))
            .Add(p => p.NotAuthorized, rebote));

        // Se espera a que la pantalla se decida por una de las dos ramas. Sin este "o", el
        // WaitForState del caso no autorizado terminaría en timeout en vez de en una aserción
        // legible.
        cut.WaitForState(() => cut.FindAll("form").Count > 0 || cut.FindAll("[data-rebote]").Count > 0);
        return cut;
    }

    private static bool HayBoton(IRenderedComponent<AuthorizeRouteView> cut, string texto) =>
        cut.FindAll("button").Any(b => b.TextContent.Trim() == texto);

    // CASO 7 (§10.1): con rol Delivery la ficha de un pedido Servido + Delivery se abre, y
    // muestra lo que el repartidor viene a buscar: qué lleva y a dónde va.
    [Fact]
    public void Ficha_ConRolDelivery_SeAbreYMuestraPlatosYElBloqueDeEntrega()
    {
        PrepararDependencias(PedidoServidoDelivery(), "Delivery");

        var cut = RenderizarFicha(IdPedido);

        // No redirige: hoy [Authorize(Roles = "Admin,Mesero")] deja a Delivery afuera y
        // AuthorizeRouteView dibuja el NotAuthorized (en producción, RedirectToLogin).
        Assert.Empty(cut.FindAll("[data-rebote]"));

        // La ficha cargó de verdad, con el pedido pedido por URL.
        Assert.Contains("Pedido 012 · Ana", cut.Find("h3").TextContent);

        // "Verificar el pedido": la lista de platos.
        Assert.Contains(
            cut.FindAll("li.list-group-item"),
            li => li.TextContent.Contains(NombrePlato));

        // "A dónde entregarlo": el bloque de Entrega y el enlace que abre la app de mapas.
        Assert.Contains(cut.FindAll("h4"), h => h.TextContent.Trim() == "Entrega");

        var enlace = cut.FindAll("a").Single(a => a.TextContent.Trim() == "Abrir para navegar");
        Assert.Equal(UbicacionCompartida.EnlaceMapa(Latitud, Longitud), enlace.GetAttribute("href"));
    }

    // CASO 8 (§10.1): la misma ficha no ofrece ninguna acción de escritura. Son aserciones de
    // AUSENCIA, y una ausencia también se cumple con la pantalla en blanco: por eso el test
    // empieza anclándose en que la ficha efectivamente cargó (mismas aserciones positivas del
    // caso 7) antes de exigir que falte cada control.
    [Fact]
    public void Ficha_ConRolDelivery_NoOfreceNingunaAccionDeEscritura()
    {
        PrepararDependencias(PedidoServidoDelivery(), "Delivery");

        var cut = RenderizarFicha(IdPedido);

        // --- Ancla: la ficha está dibujada -----------------------------------------
        Assert.Empty(cut.FindAll("[data-rebote]"));
        Assert.Contains("Pedido 012 · Ana", cut.Find("h3").TextContent);
        Assert.Contains(cut.FindAll("h4"), h => h.TextContent.Trim() == "Entrega");
        Assert.Contains(cut.FindAll("h4"), h => h.TextContent.Trim() == "Mesas del pedido");
        Assert.Single(cut.FindAll("a"), a => a.TextContent.Trim() == "Abrir para navegar");

        // --- Ausencias (§8.1) ------------------------------------------------------
        Assert.False(HayBoton(cut, "Anular"));
        Assert.False(HayBoton(cut, "Agregar mesa"));
        Assert.Empty(cut.FindAll(".pedido-barra-acciones"));
        Assert.Empty(cut.FindAll(".pedido-barra-espaciador"));

        // Cuarto punto de §8.1: ComprobantesPanel se sigue viendo (la evidencia es lectura)
        // pero sin su formulario de subida ni el botón de reemplazo.
        Assert.Contains(cut.FindAll(".card-header"), h => h.TextContent.Contains($"cuenta #{IdCuentaQr}"));
        Assert.False(HayBoton(cut, "Registrar"));
        Assert.False(HayBoton(cut, "Reemplazar"));

        // El campo Comensal es el único input dentro del <form> de la ficha.
        var comensal = cut.Find("form input.form-control");
        Assert.True(comensal.HasAttribute("disabled"));
    }

    // CASO 9 (§10.1): regresión explícita. Admin y Mesero siguen viendo las mismas acciones —
    // sin este test, el caso 8 pasaría igual si alguien apagara la pantalla para todos.
    [Theory]
    [InlineData("Admin")]
    [InlineData("Mesero")]
    public void Ficha_ConRolQueEdita_SigueOfreciendoLasAcciones(string rol)
    {
        PrepararDependencias(PedidoServidoDelivery(), rol);

        var cut = RenderizarFicha(IdPedido);

        Assert.Empty(cut.FindAll("[data-rebote]"));
        Assert.Contains("Pedido 012 · Ana", cut.Find("h3").TextContent);

        Assert.True(HayBoton(cut, "Anular"));
        Assert.True(HayBoton(cut, "Agregar mesa"));
        Assert.Single(cut.FindAll(".pedido-barra-acciones"));
        Assert.Single(cut.FindAll(".pedido-barra-espaciador"));
        Assert.True(HayBoton(cut, "Registrar"));
        Assert.True(HayBoton(cut, "Reemplazar"));

        var comensal = cut.Find("form input.form-control");
        Assert.False(comensal.HasAttribute("disabled"));
    }
}
