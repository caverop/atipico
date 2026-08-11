using Atipico.Application.Models;

namespace Atipico.Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request);
    }
}
