using Atipico.Application.Common.Interfaces;
using Atipico.Application.Services;
using Atipico.Domain.Entities;
using Atipico.Domain.Interfaces.Repositories;
using Moq;

namespace Atipico.Application.Tests
{
    public class EntityServiceTests
    {
        private readonly Mock<IRepository<Usuario>> _repositoryMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly EntityService<Usuario> _service;

        public EntityServiceTests()
        {
            _unitOfWorkMock.Setup(u => u.Repository<Usuario>()).Returns(_repositoryMock.Object);
            _service = new EntityService<Usuario>(_unitOfWorkMock.Object);
        }

        [Fact]
        public async Task GetByIdAsync_DelegatesToRepository()
        {
            var usuario = new Usuario { Id = 1, Nombre = "Ana" };
            _repositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(usuario);

            var result = await _service.GetByIdAsync(1);

            Assert.Same(usuario, result);
            _repositoryMock.Verify(r => r.GetByIdAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_DelegatesToRepository()
        {
            var usuarios = new List<Usuario> { new() { Id = 1, Nombre = "Ana" }, new() { Id = 2, Nombre = "Bruno" } };
            _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(usuarios);

            var result = await _service.GetAllAsync();

            Assert.Equal(usuarios, result);
            _repositoryMock.Verify(r => r.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task AddAsync_AddsEntityAndSavesChanges()
        {
            var usuario = new Usuario { Nombre = "Ana" };

            var result = await _service.AddAsync(usuario);

            Assert.Same(usuario, result);
            _repositoryMock.Verify(r => r.Add(usuario), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_UpdatesEntityAndSavesChanges()
        {
            var usuario = new Usuario { Id = 1, Nombre = "Ana" };

            await _service.UpdateAsync(usuario);

            _repositoryMock.Verify(r => r.Update(usuario), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RemoveAsync_RemovesEntityAndSavesChanges()
        {
            var usuario = new Usuario { Id = 1, Nombre = "Ana" };

            await _service.RemoveAsync(usuario);

            _repositoryMock.Verify(r => r.Remove(usuario), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }
    }
}
