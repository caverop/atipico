using Atipico.Api.Controllers;
using Atipico.Application.Common.Interfaces;
using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;

namespace Atipico.Api.Tests
{
    public class UsuariosControllerTests
    {
        private readonly Mock<IEntityService<Usuario>> _serviceMock = new();
        private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
        private readonly UsuariosController _controller;

        public UsuariosControllerTests()
        {
            _controller = new UsuariosController(_serviceMock.Object, _passwordHasherMock.Object);
            _passwordHasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash-generado");
            SetUser("Admin");
        }

        private void SetUser(params string[] roles)
        {
            var claims = roles.Select(r => new Claim(ClaimTypes.Role, r));
            var identity = new ClaimsIdentity(claims, "TestAuth");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        [Fact]
        public async Task GetAll_ReturnsOkWithEntities()
        {
            var usuarios = new List<Usuario> { new() { Id = 1, Nombre = "Ana" } };
            _serviceMock.Setup(s => s.GetAllAsync()).ReturnsAsync(usuarios);

            var result = await _controller.GetAll();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(usuarios, okResult.Value);
        }

        [Fact]
        public async Task GetById_Found_ReturnsOkWithEntity()
        {
            var usuario = new Usuario { Id = 1, Nombre = "Ana" };
            _serviceMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(usuario);

            var result = await _controller.GetById(1);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(usuario, okResult.Value);
        }

        [Fact]
        public async Task GetById_NotFound_ReturnsNotFound()
        {
            _serviceMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync((Usuario?)null);

            var result = await _controller.GetById(1);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task Create_SinPassword_ReturnsBadRequest()
        {
            var usuario = new Usuario { Nombre = "Ana" };

            var result = await _controller.Create(usuario);

            Assert.IsType<BadRequestObjectResult>(result.Result);
            _serviceMock.Verify(s => s.AddAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task Create_ConPassword_HasheaYReturnsCreatedAtAction()
        {
            var usuario = new Usuario { Id = 1, Nombre = "Ana", Password = "clave-temporal" };
            _serviceMock.Setup(s => s.AddAsync(usuario)).ReturnsAsync(usuario);

            var result = await _controller.Create(usuario);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(nameof(UsuariosController.GetById), createdResult.ActionName);
            Assert.Same(usuario, createdResult.Value);
            Assert.Equal(1L, createdResult.RouteValues?["id"]);
            Assert.Equal("hash-generado", usuario.PasswordHash);
            Assert.Null(usuario.Password);
            _passwordHasherMock.Verify(h => h.Hash("clave-temporal"), Times.Once);
            _serviceMock.Verify(s => s.AddAsync(usuario), Times.Once);
        }

        [Fact]
        public async Task Update_IdMismatch_ReturnsBadRequest()
        {
            var usuario = new Usuario { Id = 2, Nombre = "Ana" };

            var result = await _controller.Update(1, usuario);

            Assert.IsType<BadRequestObjectResult>(result);
            _serviceMock.Verify(s => s.GetByIdAsync(It.IsAny<long>()), Times.Never);
            _serviceMock.Verify(s => s.UpdateAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task Update_SinPassword_PreservaElHashExistenteYMutaLaInstanciaRastreada()
        {
            var existente = new Usuario { Id = 1, Nombre = "Ana", PasswordHash = "hash-original" };
            var cambios = new Usuario { Id = 1, Nombre = "Ana Editada" };
            _serviceMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(existente);

            var result = await _controller.Update(1, cambios);

            Assert.IsType<NoContentResult>(result);
            Assert.Equal("hash-original", existente.PasswordHash);
            Assert.Equal("Ana Editada", existente.Nombre);
            _passwordHasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
            // Se guarda la instancia YA rastreada (existente), nunca la que llegó del body,
            // para no chocar con el change tracker de EF (mismo id, dos instancias).
            _serviceMock.Verify(s => s.UpdateAsync(existente), Times.Once);
            _serviceMock.Verify(s => s.UpdateAsync(cambios), Times.Never);
        }

        [Fact]
        public async Task Update_ConPassword_HasheaLaNuevaContraseñaSobreLaInstanciaRastreada()
        {
            var existente = new Usuario { Id = 1, Nombre = "Ana", PasswordHash = "hash-original" };
            var cambios = new Usuario { Id = 1, Nombre = "Ana", Password = "nueva-clave" };
            _serviceMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(existente);

            var result = await _controller.Update(1, cambios);

            Assert.IsType<NoContentResult>(result);
            Assert.Equal("hash-generado", existente.PasswordHash);
            _passwordHasherMock.Verify(h => h.Hash("nueva-clave"), Times.Once);
            _serviceMock.Verify(s => s.UpdateAsync(existente), Times.Once);
        }

        [Fact]
        public async Task Update_UsuarioNoExiste_ReturnsNotFound()
        {
            var usuario = new Usuario { Id = 1, Nombre = "Ana" };
            _serviceMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync((Usuario?)null);

            var result = await _controller.Update(1, usuario);

            Assert.IsType<NotFoundResult>(result);
            _serviceMock.Verify(s => s.UpdateAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task Update_ErrorDeConcurrencia_ReturnsNotFound()
        {
            var existente = new Usuario { Id = 1, Nombre = "Ana", PasswordHash = "hash-original" };
            var cambios = new Usuario { Id = 1, Nombre = "Ana", Password = "clave" };
            _serviceMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(existente);
            _serviceMock.Setup(s => s.UpdateAsync(existente)).ThrowsAsync(new DbUpdateConcurrencyException());

            var result = await _controller.Update(1, cambios);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_Found_ReturnsNoContent()
        {
            var usuario = new Usuario { Id = 1, Nombre = "Ana" };
            _serviceMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(usuario);

            var result = await _controller.Delete(1);

            Assert.IsType<NoContentResult>(result);
            _serviceMock.Verify(s => s.RemoveAsync(usuario), Times.Once);
        }

        [Fact]
        public async Task Delete_NotFound_ReturnsNotFound()
        {
            _serviceMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync((Usuario?)null);

            var result = await _controller.Delete(1);

            Assert.IsType<NotFoundResult>(result);
            _serviceMock.Verify(s => s.RemoveAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task Create_WithoutAdminRole_ReturnsForbid()
        {
            SetUser("Mesero");
            var usuario = new Usuario { Nombre = "Ana", Password = "clave" };

            var result = await _controller.Create(usuario);

            Assert.IsType<ForbidResult>(result.Result);
            _serviceMock.Verify(s => s.AddAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task Update_WithoutAdminRole_ReturnsForbid()
        {
            SetUser("Cajero");
            var usuario = new Usuario { Id = 1, Nombre = "Ana" };

            var result = await _controller.Update(1, usuario);

            Assert.IsType<ForbidResult>(result);
            _serviceMock.Verify(s => s.UpdateAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task Delete_WithoutAdminRole_ReturnsForbid()
        {
            SetUser("Cocinero");

            var result = await _controller.Delete(1);

            Assert.IsType<ForbidResult>(result);
            _serviceMock.Verify(s => s.GetByIdAsync(It.IsAny<long>()), Times.Never);
        }
    }
}
