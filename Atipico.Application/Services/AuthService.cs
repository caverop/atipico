using Atipico.Application.Common.Interfaces;
using Atipico.Application.Interfaces.Services;
using Atipico.Application.Models;
using Atipico.Domain.Entities;

namespace Atipico.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;

        public AuthService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, IJwtTokenGenerator jwtTokenGenerator)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _jwtTokenGenerator = jwtTokenGenerator;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            var repository = _unitOfWork.Repository<Usuario>();
            var coincidencias = await repository.FindAsync(u => u.NombreUsuario == request.NombreUsuario && u.Activo);
            var usuario = coincidencias.SingleOrDefault();

            if (usuario is null || !_passwordHasher.Verify(request.Password, usuario.PasswordHash))
                return null;

            var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(usuario);

            return new LoginResponse
            {
                Token = token,
                ExpiresAt = expiresAt,
                UsuarioId = usuario.Id,
                Nombre = usuario.Nombre,
                NombreUsuario = usuario.NombreUsuario,
                Rol = usuario.Rol.ToString(),
            };
        }
    }
}
