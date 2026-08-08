using Atipico.Api.Controllers;
using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Atipico.Api.Tests
{
    public class UsuariosControllerTests
    {
        private readonly Mock<IEntityService<Usuario>> _serviceMock = new();
        private readonly UsuariosController _controller;

        public UsuariosControllerTests()
        {
            _controller = new UsuariosController(_serviceMock.Object);
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
        public async Task Create_ReturnsCreatedAtActionWithEntity()
        {
            var usuario = new Usuario { Id = 1, Nombre = "Ana" };
            _serviceMock.Setup(s => s.AddAsync(usuario)).ReturnsAsync(usuario);

            var result = await _controller.Create(usuario);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(nameof(UsuariosController.GetById), createdResult.ActionName);
            Assert.Same(usuario, createdResult.Value);
            Assert.Equal(1L, createdResult.RouteValues?["id"]);
            _serviceMock.Verify(s => s.AddAsync(usuario), Times.Once);
        }

        [Fact]
        public async Task Update_IdMismatch_ReturnsBadRequest()
        {
            var usuario = new Usuario { Id = 2, Nombre = "Ana" };

            var result = await _controller.Update(1, usuario);

            Assert.IsType<BadRequestObjectResult>(result);
            _serviceMock.Verify(s => s.UpdateAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task Update_Success_ReturnsNoContent()
        {
            var usuario = new Usuario { Id = 1, Nombre = "Ana" };

            var result = await _controller.Update(1, usuario);

            Assert.IsType<NoContentResult>(result);
            _serviceMock.Verify(s => s.UpdateAsync(usuario), Times.Once);
        }

        [Fact]
        public async Task Update_EntityDoesNotExist_ReturnsNotFound()
        {
            var usuario = new Usuario { Id = 1, Nombre = "Ana" };
            _serviceMock.Setup(s => s.UpdateAsync(usuario)).ThrowsAsync(
                new DbUpdateConcurrencyException());

            var result = await _controller.Update(1, usuario);

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
    }
}
