using Atipico.Application.Common.Interfaces;
using Atipico.Application.Models;
using Atipico.Application.Services;
using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Domain.Interfaces.Repositories;
using Moq;

namespace Atipico.Application.Tests
{
    public class AuthServiceTests
    {
        private readonly Mock<IRepository<Usuario>> _repositoryMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
        private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock = new();
        private readonly AuthService _service;

        public AuthServiceTests()
        {
            _unitOfWorkMock.Setup(u => u.Repository<Usuario>()).Returns(_repositoryMock.Object);
            _service = new AuthService(_unitOfWorkMock.Object, _passwordHasherMock.Object, _jwtTokenGeneratorMock.Object);
        }

        private static Usuario CrearUsuario() => new()
        {
            Id = 1,
            Nombre = "Ana Quispe",
            NombreUsuario = "ana.quispe",
            PasswordHash = "hash-almacenado",
            Rol = RolUsuario.Mesero,
            Activo = true,
        };

        [Fact]
        public async Task LoginAsync_CredencialesValidas_DevuelveToken()
        {
            var usuario = CrearUsuario();
            var request = new LoginRequest { NombreUsuario = "ana.quispe", Password = "clave-correcta" };

            _repositoryMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Usuario, bool>>>()))
                .ReturnsAsync([usuario]);
            _passwordHasherMock.Setup(h => h.Verify(request.Password, usuario.PasswordHash)).Returns(true);

            var expiresAt = DateTimeOffset.UtcNow.AddHours(8);
            _jwtTokenGeneratorMock.Setup(j => j.GenerateToken(usuario)).Returns(("token-generado", expiresAt));

            var result = await _service.LoginAsync(request);

            Assert.NotNull(result);
            Assert.Equal("token-generado", result!.Token);
            Assert.Equal(expiresAt, result.ExpiresAt);
            Assert.Equal(usuario.Id, result.UsuarioId);
            Assert.Equal(usuario.NombreUsuario, result.NombreUsuario);
            Assert.Equal(nameof(RolUsuario.Mesero), result.Rol);
        }

        [Fact]
        public async Task LoginAsync_UsuarioNoExiste_DevuelveNull()
        {
            var request = new LoginRequest { NombreUsuario = "no.existe", Password = "cualquiera" };

            _repositoryMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Usuario, bool>>>()))
                .ReturnsAsync([]);

            var result = await _service.LoginAsync(request);

            Assert.Null(result);
            _jwtTokenGeneratorMock.Verify(j => j.GenerateToken(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_ContraseñaIncorrecta_DevuelveNull()
        {
            var usuario = CrearUsuario();
            var request = new LoginRequest { NombreUsuario = "ana.quispe", Password = "clave-incorrecta" };

            _repositoryMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Usuario, bool>>>()))
                .ReturnsAsync([usuario]);
            _passwordHasherMock.Setup(h => h.Verify(request.Password, usuario.PasswordHash)).Returns(false);

            var result = await _service.LoginAsync(request);

            Assert.Null(result);
            _jwtTokenGeneratorMock.Verify(j => j.GenerateToken(It.IsAny<Usuario>()), Times.Never);
        }
    }
}
