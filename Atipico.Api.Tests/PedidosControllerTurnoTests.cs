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
        // Spec §6.1 (specs/numero-mesa-grilla-pedidos.md): promovidos desde
        // `new Mock<...>().Object` inline a campos con Setup por defecto. Sin este Setup,
        // en cuanto ArmarGrillaAsync los use (MesasPorPedidoAsync), Moq devuelve null en vez
        // de una lista vacía y la llamada revienta con NullReferenceException antes de llegar
        // a ninguna aserción — no es el escenario que estos tests quieren cubrir.
        private readonly Mock<IEntityService<PedidoMesa>> _pedidoMesas = new();
        private readonly Mock<IEntityService<Mesa>> _mesas = new();
        private readonly PedidosController _controller;

        public PedidosControllerTurnoTests()
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

            _pedidos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<Pedido, bool>>>()))
                .ReturnsAsync([]);
            _usuarios.Setup(s => s.GetByIdAsync(It.IsAny<long>()))
                .ReturnsAsync(new Usuario { Id = 7, Nombre = "M. Ríos" });
            _pedidoMesas.Setup(s => s.FindAsync(It.IsAny<Expression<Func<PedidoMesa, bool>>>()))
                .ReturnsAsync([]);
            _mesas.Setup(s => s.GetAllAsync()).ReturnsAsync([]);

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

        // ---- Spec §6.2 numero-mesa-grilla-pedidos.md: MesasPorPedido en el sobre ----------
        //
        // Estos tests hoy NO COMPILAN: GrillaPedidosDto todavía tiene dos parámetros
        // (Turno, Pedidos) — MesasPorPedido se agrega recién en la implementación (spec §3).
        // Ese es el rojo esperado: un error de compilación en todo el proyecto de test, no
        // una aserción fallida. Ver informe.

        // Decisión documentada (spec §6.2 la deja abierta): un pedido sin PedidoMesa asociada
        // NO TIENE ENTRADA en el diccionario, en vez de tener una entrada con lista vacía. Se
        // elige "sin entrada" porque es el comportamiento natural del algoritmo que ya propone
        // el spec (§3.1): MesasPorPedidoAsync arma el diccionario agrupando
        // (`GroupBy`) sobre los PedidoMesa que existen, así que un pedido sin ninguno nunca
        // genera un grupo ni, por lo tanto, una clave — no hace falta código extra para
        // "excluirlo a propósito". El consumidor ya está preparado para este caso
        // (`GetValueOrDefault`, spec §3 y §4.1).
        [Fact]
        public async Task GetDelTurnoAbierto_PedidoSinMesaAsociada_NoTraeEntradaEnMesasPorPedido()
        {
            ConTurnoAbierto();
            _pedidos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<Pedido, bool>>>()))
                .ReturnsAsync([new Pedido { Id = 40, NumeroTurno = 1 }]);
            // Sin Setup adicional en _pedidoMesas: sigue devolviendo [] (default del ctor).

            var ok = Assert.IsType<OkObjectResult>((await _controller.GetDelTurnoAbierto()).Result);
            var grilla = Assert.IsType<GrillaPedidosDto>(ok.Value);

            Assert.False(grilla.MesasPorPedido.ContainsKey(40));
        }

        [Fact]
        public async Task GetDelTurnoAbierto_PedidoConUnaMesa_TraeElNumeroDeEsaMesa()
        {
            ConTurnoAbierto();
            _pedidos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<Pedido, bool>>>()))
                .ReturnsAsync([new Pedido { Id = 41, NumeroTurno = 1 }]);
            _pedidoMesas.Setup(s => s.FindAsync(It.IsAny<Expression<Func<PedidoMesa, bool>>>()))
                .ReturnsAsync([new PedidoMesa { Id = 1, IdPedido = 41, IdMesa = 100 }]);
            _mesas.Setup(s => s.GetAllAsync())
                .ReturnsAsync([new Mesa { Id = 100, Numero = 5 }]);

            var ok = Assert.IsType<OkObjectResult>((await _controller.GetDelTurnoAbierto()).Result);
            var grilla = Assert.IsType<GrillaPedidosDto>(ok.Value);

            Assert.Equal([5], grilla.MesasPorPedido[41]);
        }

        // Las PedidoMesa se devuelven a propósito en el orden de inserción "8 antes que 3":
        // el orden ascendente tiene que salir del cruce con Mesa.Numero, no de heredar el
        // orden en que FindAsync trajo las filas.
        [Fact]
        public async Task GetDelTurnoAbierto_PedidoConDosMesas_VienenOrdenadasAscendentePorNumero()
        {
            ConTurnoAbierto();
            _pedidos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<Pedido, bool>>>()))
                .ReturnsAsync([new Pedido { Id = 42, NumeroTurno = 1 }]);
            _pedidoMesas.Setup(s => s.FindAsync(It.IsAny<Expression<Func<PedidoMesa, bool>>>()))
                .ReturnsAsync([
                    new PedidoMesa { Id = 1, IdPedido = 42, IdMesa = 200 }, // mesa n° 8, insertada primero
                    new PedidoMesa { Id = 2, IdPedido = 42, IdMesa = 201 }, // mesa n° 3, insertada después
                ]);
            _mesas.Setup(s => s.GetAllAsync())
                .ReturnsAsync([
                    new Mesa { Id = 200, Numero = 8 },
                    new Mesa { Id = 201, Numero = 3 },
                ]);

            var ok = Assert.IsType<OkObjectResult>((await _controller.GetDelTurnoAbierto()).Result);
            var grilla = Assert.IsType<GrillaPedidosDto>(ok.Value);

            Assert.Equal([3, 8], grilla.MesasPorPedido[42]);
        }

        // Dos pedidos del mismo turno, cada uno con su propia mesa: el filtro por
        // idsPedidoSet (spec §3.1) no puede cruzar los grupos entre sí.
        [Fact]
        public async Task GetDelTurnoAbierto_DosPedidosCadaUnoConSuPropiaMesa_NoSeCruzanEntreSi()
        {
            ConTurnoAbierto();
            _pedidos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<Pedido, bool>>>()))
                .ReturnsAsync([
                    new Pedido { Id = 43, NumeroTurno = 1 },
                    new Pedido { Id = 44, NumeroTurno = 2 },
                ]);
            _pedidoMesas.Setup(s => s.FindAsync(It.IsAny<Expression<Func<PedidoMesa, bool>>>()))
                .ReturnsAsync([
                    new PedidoMesa { Id = 1, IdPedido = 43, IdMesa = 300 },
                    new PedidoMesa { Id = 2, IdPedido = 44, IdMesa = 301 },
                ]);
            _mesas.Setup(s => s.GetAllAsync())
                .ReturnsAsync([
                    new Mesa { Id = 300, Numero = 2 },
                    new Mesa { Id = 301, Numero = 9 },
                ]);

            var ok = Assert.IsType<OkObjectResult>((await _controller.GetDelTurnoAbierto()).Result);
            var grilla = Assert.IsType<GrillaPedidosDto>(ok.Value);

            Assert.Equal([2], grilla.MesasPorPedido[43]);
            Assert.Equal([9], grilla.MesasPorPedido[44]);
        }
    }
}
