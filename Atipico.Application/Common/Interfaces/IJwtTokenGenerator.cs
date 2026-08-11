using Atipico.Domain.Entities;

namespace Atipico.Application.Common.Interfaces
{
    public interface IJwtTokenGenerator
    {
        (string Token, DateTimeOffset ExpiresAt) GenerateToken(Usuario usuario);
    }
}
