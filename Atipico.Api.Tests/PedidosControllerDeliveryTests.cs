using Atipico.Api.Controllers;
using Atipico.Application.Interfaces.Services;
using Atipico.Application.Models;
using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Atipico.Api.Tests
{
    // SCRUM-13 (specs/rol-delivery.md §2). Delivery es el primer rol con una restriccion de
    // LECTURA: un usuario cuyo unico rol es Delivery solo debe ver, de Pedido, los que tienen
    // Estado = Servido y Tipo = Delivery. El corte se escribe como "no tiene ninguno de los
    // roles mas amplios" (capacidad, no identidad) — mismo criterio que ya usa
    // PedidosControllerTurnoTests.GetDelTurno_MeseroQueTambienEsAdmin_Pasa.
    //
    // Archivo separado de PedidosControllerTurnoTests.cs (que ya es largo y cubre otro tema,
    // el acotado por turno) en vez de agregarlo ahi: menos invasivo, mismo patron de
    // mocks/ConRol reutilizado tal cual.
    //
    // Spec §2.2: hoy (antes de la implementacion) EntityControllerBase<TEntity>.GetAll()/
    // GetById() NO son virtual y PedidosController no los sobrescribe — es la base sin filtrar
    // la que responde. Estos tests llaman a esos metodos ya existentes (compilan sin ningun
    // stub nuevo) y quedan en ROJO POR ASERCION (no por error de compilacion): la lista/entidad
    // que devuelven hoy no esta filtrada por rol. Confirmado en el informe.
    public class PedidosControllerDeliveryTests
    {
        private readonly Mock<IEntityService<Pedido>> _pedidos = new();
        private readonly Mock<IEntityService<TurnoCaja>> _turnos = new();
        private readonly Mock<IEntityService<Usuario>> _usuarios = new();
        private readonly Mock<IEntityService<PedidoMesa>> _pedidoMesas = new();
        private readonly Mock<IEntityService<Mesa>> _mesas = new();
        private readonly PedidosController _controller;

        public PedidosControllerDeliveryTests()
        {
            _controller = new PedidosController(
                _pedidos.Object,
                new Mock<IEntityService<PedidoPlatoSinCobrar>>().Object,
                _pedidoMesas.Object,
                _mesas.Object,
                new Mock<IEntityService<PedidoPlato>>().Object,
                new Mock<IEntityService<Plato>>().Object,
                new Mock<IEntityService<Cuenta>>().Object,
                new Mock<IEntityService<DetalleCuenta>>().Object,
                _turnos.Object,
                _usuarios.Object);

            _pedidoMesas.Setup(s => s.FindAsync(It.IsAny<Expression<Func<PedidoMesa, bool>>>()))
                .ReturnsAsync([]);
            _mesas.Setup(s => s.GetAllAsync()).ReturnsAsync([]);
            _usuarios.Setup(s => s.GetByIdAsync(It.IsAny<long>()))
                .ReturnsAsync(new Usuario { Id = 7, Nombre = "M. Ríos" });
        }

        private void ConRol(params string[] roles)
        {
            var identity = new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "TestAuth");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        // La mezcla: cumple (Servido + Delivery), estado correcto pero tipo distinto, tipo
        // correcto pero estado distinto, y ninguno de los dos.
        private static List<Pedido> PedidosMixtos() =>
        [
            new Pedido { Id = 1, Estado = EstadoPedido.Servido, Tipo = TipoPedido.Delivery },   // cumple
            new Pedido { Id = 2, Estado = EstadoPedido.Servido, Tipo = TipoPedido.EnSalon },     // estado ok, tipo no
            new Pedido { Id = 3, Estado = EstadoPedido.Abierto, Tipo = TipoPedido.Delivery },    // tipo ok, estado no
            new Pedido { Id = 4, Estado = EstadoPedido.Cerrado, Tipo = TipoPedido.ParaLlevar },  // ninguno
            new Pedido { Id = 5, Estado = EstadoPedido.Servido, Tipo = TipoPedido.Delivery },    // cumple
        ];

        // ---- Caso 1 (spec §10.1) --------------------------------------------------------
        [Fact]
        public async Task GetAll_ConRolDelivery_DevuelveSoloServidoYDelivery()
        {
            _pedidos.Setup(s => s.GetAllAsync()).ReturnsAsync(PedidosMixtos());
            ConRol("Delivery");

            var ok = Assert.IsType<OkObjectResult>((await _controller.GetAll()).Result);
            var visibles = Assert.IsAssignableFrom<IEnumerable<Pedido>>(ok.Value).ToList();

            Assert.Equal([1L, 5L], visibles.Select(p => p.Id).OrderBy(id => id));
        }

        // ---- Caso 2 (spec §10.2): regresion, el filtro no se activa de mas -----------------
        [Theory]
        [InlineData("Admin")]
        [InlineData("Mesero")]
        public async Task GetAll_ConRolQueNoEsSoloDelivery_DevuelveTodoSinFiltrar(string rol)
        {
            var todos = PedidosMixtos();
            _pedidos.Setup(s => s.GetAllAsync()).ReturnsAsync(todos);
            ConRol(rol);

            var ok = Assert.IsType<OkObjectResult>((await _controller.GetAll()).Result);
            var visibles = Assert.IsAssignableFrom<IEnumerable<Pedido>>(ok.Value).ToList();

            Assert.Equal(todos.Count, visibles.Count);
        }

        // ---- Caso 3 (spec §10.3): NotFound, no Forbid --------------------------------------
        [Fact]
        public async Task GetById_ConRolDelivery_PedidoQueNoCumple_NotFound()
        {
            var pedido = new Pedido { Id = 10, Estado = EstadoPedido.Abierto, Tipo = TipoPedido.EnSalon };
            _pedidos.Setup(s => s.GetByIdAsync(10)).ReturnsAsync(pedido);
            ConRol("Delivery");

            var resultado = (await _controller.GetById(10)).Result;

            Assert.IsType<NotFoundResult>(resultado);
        }

        // ---- Caso 4 (spec §10.4) ------------------------------------------------------------
        [Fact]
        public async Task GetById_ConRolDelivery_PedidoQueCumple_Ok()
        {
            var pedido = new Pedido { Id = 11, Estado = EstadoPedido.Servido, Tipo = TipoPedido.Delivery };
            _pedidos.Setup(s => s.GetByIdAsync(11)).ReturnsAsync(pedido);
            ConRol("Delivery");

            var ok = Assert.IsType<OkObjectResult>((await _controller.GetById(11)).Result);

            Assert.Same(pedido, ok.Value);
        }

        // ---- Caso 5 (spec §10.5): GetDelTurnoAbierto / ArmarGrillaAsync ---------------------
        [Fact]
        public async Task GetDelTurnoAbierto_ConRolDelivery_SoloTraeLosPedidosFiltradosYTotalPedidosLosCuentaAEllos()
        {
            var turno = new TurnoCaja { Id = 5, Nombre = "Ejecutivo", IdCajero = 7, AbiertoEn = DateTimeOffset.UtcNow };
            _turnos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<TurnoCaja, bool>>>()))
                .ReturnsAsync([turno]);

            _pedidos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<Pedido, bool>>>()))
                .ReturnsAsync(PedidosMixtos());
            ConRol("Delivery");

            var ok = Assert.IsType<OkObjectResult>((await _controller.GetDelTurnoAbierto()).Result);
            var grilla = Assert.IsType<GrillaPedidosDto>(ok.Value);

            Assert.Equal([1L, 5L], grilla.Pedidos.Select(p => p.Id).OrderBy(id => id));
            // El total sale de la lista que ya llego a la pantalla (2), no del turno real (5).
            Assert.Equal(2, grilla.Turno!.TotalPedidos);
        }
    }
}
