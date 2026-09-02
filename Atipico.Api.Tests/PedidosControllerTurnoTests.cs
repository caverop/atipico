using Atipico.Api.Controllers;
using Atipico.Application.Interfaces.Services;
using Atipico.Application.Models;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Atipico.Api.Tests
{
    // Las rutas de pedidos acotadas al turno. Ver specs/numero-pedido.md §7.3 y §8.0.
    public class PedidosControllerTurnoTests
    {
        private readonly Mock<IEntityService<Pedido>> _pedidos = new();
        private readonly Mock<IEntityService<TurnoCaja>> _turnos = new();
        private readonly Mock<IEntityService<Usuario>> _usuarios = new();
        private readonly PedidosController _controller;

        public PedidosControllerTurnoTests()
        {
            _controller = new PedidosController(
                _pedidos.Object,
                new Mock<IEntityService<PedidoPlatoSinCobrar>>().Object,
                new Mock<IEntityService<PedidoMesa>>().Object,
                new Mock<IEntityService<Mesa>>().Object,
                new Mock<IEntityService<PedidoPlato>>().Object,
                new Mock<IEntityService<Plato>>().Object,
                new Mock<IEntityService<Cuenta>>().Object,
                new Mock<IEntityService<DetalleCuenta>>().Object,
                _turnos.Object,
                _usuarios.Object);

            _pedidos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<Pedido, bool>>>()))
                .ReturnsAsync([]);
            _usuarios.Setup(s => s.GetByIdAsync(It.IsAny<long>()))
                .ReturnsAsync(new Usuario { Id = 7, Nombre = "M. Ríos" });

            ConRol("Admin");
        }

        private void ConRol(params string[] roles)
        {
            var identity = new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "TestAuth");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        private TurnoCaja ConTurnoAbierto()
        {
            var turno = new TurnoCaja { Id = 5, Nombre = "Ejecutivo", IdCajero = 7, AbiertoEn = DateTimeOffset.UtcNow };
            _turnos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<TurnoCaja, bool>>>()))
                .ReturnsAsync([turno]);
            _turnos.Setup(s => s.GetByIdAsync(turno.Id)).ReturnsAsync(turno);
            return turno;
        }

        // §8.4: sin turno abierto no hay grilla, pero tampoco es un error — es el estado en
        // que queda la base recien migrada. El sobre lo dice con Turno en null para que la
        // pantalla ofrezca abrir uno en vez de dibujar una tabla vacia.
        [Fact]
        public async Task GetDelTurnoAbierto_SinTurno_DevuelveSobreVacioYNoUnError()
        {
            _turnos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<TurnoCaja, bool>>>()))
                .ReturnsAsync([]);

            var ok = Assert.IsType<OkObjectResult>((await _controller.GetDelTurnoAbierto()).Result);
            var grilla = Assert.IsType<GrillaPedidosDto>(ok.Value);

            Assert.Null(grilla.Turno);
            Assert.Empty(grilla.Pedidos);
        }

        // RN-11: el alcance es el turno, no el estado. Un pedido cerrado y uno anulado siguen
        // en la lista; los chips de estado son un filtro del usuario DENTRO de ella.
        [Fact]
        public async Task GetDelTurnoAbierto_TraeTodosLosPedidosDelTurnoSinFiltrarPorEstado()
        {
            var turno = ConTurnoAbierto();
            _pedidos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<Pedido, bool>>>()))
                .ReturnsAsync([
                    new Pedido { Id = 1, NumeroTurno = 1, Estado = Domain.Enums.EstadoPedido.Cerrado },
                    new Pedido { Id = 2, NumeroTurno = 2, Estado = Domain.Enums.EstadoPedido.Anulado },
                    new Pedido { Id = 3, NumeroTurno = 3, Estado = Domain.Enums.EstadoPedido.Servido },
                ]);

            var ok = Assert.IsType<OkObjectResult>((await _controller.GetDelTurnoAbierto()).Result);
            var grilla = Assert.IsType<GrillaPedidosDto>(ok.Value);

            Assert.Equal(3, grilla.Pedidos.Count);
            Assert.Equal(turno.Id, grilla.Turno!.Id);
            // El total sale de la lista que ya se trajo, no de un COUNT aparte.
            Assert.Equal(3, grilla.Turno.TotalPedidos);
        }

        // RN-12 / CA-25. La grilla ya le esconde el filtro al mesero, pero eso es maquetado:
        // para el que canta "el 12" en el salon el numero tiene que ser inequivoco, y deja de
        // serlo apenas la lista abarca dos turnos. Esto lo hace cierto tambien si alguien
        // llama la ruta a mano.
        [Fact]
        public async Task GetDelTurno_ComoMesero_Forbid()
        {
            ConTurnoAbierto();
            ConRol("Mesero");

            Assert.IsType<ForbidResult>((await _controller.GetDelTurno(5)).Result);
        }

        [Theory]
        [InlineData("Admin")]
        [InlineData("Cajero")]
        [InlineData("Cocinero")]
        public async Task GetDelTurno_ConRolQuePuedeAmpliar_DevuelveElTurno(string rol)
        {
            ConTurnoAbierto();
            ConRol(rol);

            var ok = Assert.IsType<OkObjectResult>((await _controller.GetDelTurno(5)).Result);
            Assert.Equal(5, Assert.IsType<GrillaPedidosDto>(ok.Value).Turno!.Id);
        }

        // Un mesero que ademas es Admin (rol acumulado) si puede: el corte es por capacidad,
        // no por "ser mesero".
        [Fact]
        public async Task GetDelTurno_MeseroQueTambienEsAdmin_Pasa()
        {
            ConTurnoAbierto();
            ConRol("Mesero", "Admin");

            Assert.IsType<OkObjectResult>((await _controller.GetDelTurno(5)).Result);
        }

        [Fact]
        public async Task GetDelTurno_TurnoInexistente_NotFound()
        {
            _turnos.Setup(s => s.GetByIdAsync(It.IsAny<long>())).ReturnsAsync((TurnoCaja?)null);

            Assert.IsType<NotFoundResult>((await _controller.GetDelTurno(99)).Result);
        }
    }
}
