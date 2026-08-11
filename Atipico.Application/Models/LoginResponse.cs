namespace Atipico.Application.Models
{
    public class LoginResponse
    {
        public string Token { get; set; } = null!;
        public DateTimeOffset ExpiresAt { get; set; }
        public long UsuarioId { get; set; }
        public string Nombre { get; set; } = null!;
        public string NombreUsuario { get; set; } = null!;
        public string Rol { get; set; } = null!;
    }
}
