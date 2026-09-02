using Atipico.Api.Controllers;
using Atipico.Application.Interfaces.Services;
using Atipico.Application.Models;
using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Atipico.Api.Tests
{
    // Ver specs/numero-pedido.md §7.2. Lo que se prueba aca es la parte que vive en C#: quien
    // puede abrir y cerrar, que el turno vigente NO se cierre si la apertura va a fallar, y
    // que el rechazo por pedidos vivos traiga la lista y concuerde en numero.
    //
    // Lo que NO se prueba aca —porque no vive aca— es la asignacion del correlativo y el
    // bloqueo del cierre: eso son triggers, y un IEntityService mockeado no los ejecuta.
    // Verificado a mano contra PostgreSQL 17 (CA-1 a CA-20).
    public class TurnosControllerTests
    {
        private const long IdUsuario = 7;

        private readonly Mock<IEntityService<TurnoCaja>> _turnos = new();
        private readonly Mock<IEntityService<Pedido>> _pedidos = new();
        private readonly Mock<IEntityService<Usuario>> _usuarios = new();
        private readonly TurnosController _controller;

        public TurnosControllerTests()
        {
            _controller = new TurnosController(_turnos.Object, _pedidos.Object, _usuarios.Object);

            SinTurnoAbierto();
            SinPedidosVivos();
            _turnos.Setup(s => s.AddAsync(It.IsAny<TurnoCaja>()))
                .ReturnsAsync((TurnoCaja t) => t);
            _usuarios.Setup(s => s.GetByIdAsync(It.IsAny<long>()))
                .ReturnsAsync(new Usuario { Id = IdUsuario, Nombre = "M. Ríos" });

            ConRol("Cajero");
        }

        private void ConRol(params string[] roles)
        {
            var claims = roles.Select(r => new Claim(ClaimTypes.Role, r))
                .Append(new Claim(ClaimTypes.NameIdentifier, IdUsuario.ToString()));
            var identity = new ClaimsIdentity(claims, "TestAuth");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        private void SinTurnoAbierto() =>
            _turnos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<TurnoCaja, bool>>>()))
                .ReturnsAsync([]);

        private TurnoCaja ConTurnoAbierto(string nombre = "Ejecutivo")
        {
            var turno = new TurnoCaja { Id = 3, Nombre = nombre, IdCajero = IdUsuario, AbiertoEn = DateTimeOffset.UtcNow };
            _turnos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<TurnoCaja, bool>>>()))
                .ReturnsAsync([turno]);
            return turno;
        }

        private void SinPedidosVivos() =>
            _pedidos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<Pedido, bool>>>()))
                .ReturnsAsync([]);

        private void ConPedidosVivos(params Pedido[] pedidos) =>
            _pedidos.Setup(s => s.FindAsync(It.IsAny<Expression<Func<Pedido, bool>>>()))
                .ReturnsAsync(pedidos);

        private static string MensajeDe(ObjectResult resultado)
        {
            var valor = resultado.Value!;
            if (valor is CierreBloqueadoDto bloqueo)
                return bloqueo.Message;

            return (string)valor.GetType().GetProperty("message")!.GetValue(valor)!;
        }

        // ---- Abrir -----------------------------------------------------------------------

        [Fact]
        public async Task Abrir_SinTurnoVigente_CreaElTurnoConElCajeroDelToken()
        {
            var resultado = await _controller.Abrir(new AbrirTurnoRequest { Nombre = "A la carta" });

            Assert.IsType<CreatedAtActionResult>(resultado.Result);
            _turnos.Verify(s => s.AddAsync(It.Is<TurnoCaja>(t =>
                t.Nombre == "A la carta" && t.IdCajero == IdUsuario)), Times.Once);
        }

        // El cajero sale del token, nunca del cuerpo: si viniera del cliente, cualquiera
        // podria abrir un turno a nombre de otro. Mismo criterio que ComprobantesController.
        [Fact]
        public async Task Abrir_SinClaimDeUsuario_NoInventaCajero()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, "Cajero")], "TestAuth");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            await _controller.Abrir(new AbrirTurnoRequest { Nombre = "A la carta" });

            _turnos.Verify(s => s.AddAsync(It.Is<TurnoCaja>(t => t.IdCajero == null)), Times.Once);
        }

        [Fact]
        public async Task Abrir_ConTurnoVigente_LoCierraYAbreElNuevo()
        {
            var vigente = ConTurnoAbierto();

            await _controller.Abrir(new AbrirTurnoRequest { Nombre = "A la carta" });

            _turnos.Verify(s => s.UpdateAsync(It.Is<TurnoCaja>(t => t.Id == vigente.Id && t.CerradoEn != null)), Times.Once);
            _turnos.Verify(s => s.AddAsync(It.IsAny<TurnoCaja>()), Times.Once);
        }

        [Fact]
        public async Task Abrir_RecortaElNombre()
        {
            await _controller.Abrir(new AbrirTurnoRequest { Nombre = "  Ejecutivo  " });

            _turnos.Verify(s => s.AddAsync(It.Is<TurnoCaja>(t => t.Nombre == "Ejecutivo")), Times.Once);
        }

        // El nombre se valida ANTES de tocar el turno vigente. ck_turno_caja_nombre lo
        // rechazaria igual, pero para entonces el anterior ya estaria cerrado y la caja
        // quedaria sin ninguno abierto — con lo cual todo pedido nuevo se rechaza (RN-6).
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Abrir_ConNombreVacio_NoCierraElVigente(string nombre)
        {
            ConTurnoAbierto();

            var resultado = await _controller.Abrir(new AbrirTurnoRequest { Nombre = nombre });

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
            _turnos.Verify(s => s.UpdateAsync(It.IsAny<TurnoCaja>()), Times.Never);
            _turnos.Verify(s => s.AddAsync(It.IsAny<TurnoCaja>()), Times.Never);
        }

        [Fact]
        public async Task Abrir_ConNombreDemasiadoLargo_NoCierraElVigente()
        {
            ConTurnoAbierto();

            var resultado = await _controller.Abrir(new AbrirTurnoRequest { Nombre = new string('x', 41) });

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
            _turnos.Verify(s => s.UpdateAsync(It.IsAny<TurnoCaja>()), Times.Never);
        }

        [Fact]
        public async Task Abrir_ConPedidosVivos_NoCierraNiAbre()
        {
            ConTurnoAbierto();
            ConPedidosVivos(new Pedido { Id = 1, NumeroTurno = 12, Comensal = "Mesa 4", Estado = EstadoPedido.Servido });

            var resultado = await _controller.Abrir(new AbrirTurnoRequest { Nombre = "A la carta" });

            Assert.IsType<ConflictObjectResult>(resultado.Result);
            _turnos.Verify(s => s.UpdateAsync(It.IsAny<TurnoCaja>()), Times.Never);
            _turnos.Verify(s => s.AddAsync(It.IsAny<TurnoCaja>()), Times.Never);
        }

        [Theory]
        [InlineData("Mesero")]
        [InlineData("Cocinero")]
        public async Task Abrir_SinRolDeCaja_Forbid(string rol)
        {
            ConRol(rol);

            var resultado = await _controller.Abrir(new AbrirTurnoRequest { Nombre = "A la carta" });

            Assert.IsType<ForbidResult>(resultado.Result);
            _turnos.Verify(s => s.AddAsync(It.IsAny<TurnoCaja>()), Times.Never);
        }

        // uk_turno_caja_abierto es la ultima palabra sobre "un solo turno abierto", y su
        // violacion tiene que llegar como 409 en español, no como 500 con stack trace.
        [Fact]
        public async Task Abrir_SiLaBaseRechazaPorUnicidad_DevuelveElMensajeTraducido()
        {
            _turnos.Setup(s => s.AddAsync(It.IsAny<TurnoCaja>()))
                .ThrowsAsync(new DbUpdateException("x", ErrorDePostgres("23505", "uk_turno_caja_abierto")));

            var resultado = await _controller.Abrir(new AbrirTurnoRequest { Nombre = "A la carta" });

            var conflicto = Assert.IsType<ConflictObjectResult>(resultado.Result);
            Assert.Contains("turno de caja abierto", MensajeDe(conflicto));
        }

        // ---- Cerrar ----------------------------------------------------------------------

        [Fact]
        public async Task Cerrar_SinPedidosVivos_CierraYEstampaLaFecha()
        {
            var vigente = ConTurnoAbierto();

            var resultado = await _controller.Cerrar();

            Assert.IsType<NoContentResult>(resultado);
            _turnos.Verify(s => s.UpdateAsync(It.Is<TurnoCaja>(t => t.Id == vigente.Id && t.CerradoEn != null)), Times.Once);
        }

        // La fecha nunca llega del cliente: el navegador arma el DateTimeOffset con su propio
        // huso y Npgsql solo acepta offset 0 para timestamptz.
        [Fact]
        public async Task Cerrar_EstampaLaFechaEnUtc()
        {
            var vigente = ConTurnoAbierto();

            await _controller.Cerrar();

            Assert.Equal(TimeSpan.Zero, vigente.CerradoEn!.Value.Offset);
        }

        [Fact]
        public async Task Cerrar_SinTurnoAbierto_Conflict()
        {
            var resultado = await _controller.Cerrar();

            var conflicto = Assert.IsType<ConflictObjectResult>(resultado);
            Assert.Contains("No hay ningún turno", MensajeDe(conflicto));
            _turnos.Verify(s => s.UpdateAsync(It.IsAny<TurnoCaja>()), Times.Never);
        }

        // El mensaje sale tal cual en la pantalla del cajero: tiene que concordar en numero.
        [Fact]
        public async Task Cerrar_ConUnPedidoVivo_MensajeEnSingular()
        {
            ConTurnoAbierto();
            ConPedidosVivos(new Pedido { Id = 1, NumeroTurno = 12, Comensal = "Mesa 4", Estado = EstadoPedido.Abierto });

            var conflicto = Assert.IsType<ConflictObjectResult>(await _controller.Cerrar());

            Assert.Equal("No se puede cerrar el turno: queda 1 pedido sin cerrar.", MensajeDe(conflicto));
        }

        [Fact]
        public async Task Cerrar_ConVariosPedidosVivos_MensajeEnPluralYConLaLista()
        {
            ConTurnoAbierto();
            ConPedidosVivos(
                new Pedido { Id = 2, NumeroTurno = 12, Comensal = "Mesa 4", Estado = EstadoPedido.Servido },
                new Pedido { Id = 1, NumeroTurno = 3, Comensal = "Mesa 1", Estado = EstadoPedido.Abierto });

            var conflicto = Assert.IsType<ConflictObjectResult>(await _controller.Cerrar());
            var bloqueo = Assert.IsType<CierreBloqueadoDto>(conflicto.Value);

            Assert.Equal("No se puede cerrar el turno: quedan 2 pedidos sin cerrar.", bloqueo.Message);
            // Ordenados por numero: el cajero los va a buscar en ese orden, no en el que
            // haya devuelto la base.
            Assert.Equal([3, 12], bloqueo.Pedidos.Select(p => p.NumeroTurno));
            Assert.Equal(["Mesa 1", "Mesa 4"], bloqueo.Pedidos.Select(p => p.Comensal));
        }

        [Theory]
        [InlineData("Mesero")]
        [InlineData("Cocinero")]
        public async Task Cerrar_SinRolDeCaja_Forbid(string rol)
        {
            ConTurnoAbierto();
            ConRol(rol);

            Assert.IsType<ForbidResult>(await _controller.Cerrar());
            _turnos.Verify(s => s.UpdateAsync(It.IsAny<TurnoCaja>()), Times.Never);
        }

        // ---- Consultas -------------------------------------------------------------------

        // 204 y no 404: "no hay turno abierto" es un estado normal del sistema —el que queda
        // despues de la migracion y al terminar la jornada—, no un recurso que falta.
        [Fact]
        public async Task GetAbierto_SinTurno_NoContent()
        {
            Assert.IsType<NoContentResult>((await _controller.GetAbierto()).Result);
        }

        [Fact]
        public async Task GetAbierto_ConTurno_TraeCajeroYTotal()
        {
            ConTurnoAbierto("A la carta");
            ConPedidosVivos(new Pedido { Id = 1 }, new Pedido { Id = 2 }, new Pedido { Id = 3 });

            var ok = Assert.IsType<OkObjectResult>((await _controller.GetAbierto()).Result);
            var dto = Assert.IsType<TurnoDto>(ok.Value);

            Assert.Equal("A la carta", dto.Nombre);
            Assert.Equal("M. Ríos", dto.CajeroNombre);
            Assert.Equal(3, dto.TotalPedidos);
            Assert.Null(dto.CerradoEn);
        }

        private static PostgresException ErrorDePostgres(string sqlState, string constraintName) =>
            new(messageText: "detalle de la base", severity: "ERROR", invariantSeverity: "ERROR",
                sqlState: sqlState, constraintName: constraintName);
    }
}
