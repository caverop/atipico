namespace Atipico.Application.Models
{
    public class LoginRequest
    {
        public string NombreUsuario { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
