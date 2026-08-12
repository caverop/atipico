namespace Atipico.Web.Services
{
    // Lanzada por EntityApiClient cuando la API responde con un error: el mensaje ya viene
    // listo para mostrar al usuario (leído del cuerpo JSON, o uno genérico según el status).
    public class ApiException : Exception
    {
        public ApiException(string message) : base(message)
        {
        }
    }
}
