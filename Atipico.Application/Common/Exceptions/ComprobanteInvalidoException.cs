namespace Atipico.Application.Common.Exceptions
{
    /// <summary>
    /// El contenido subido no sirve como comprobante: no es una imagen de un formato
    /// aceptado, o esta corrupto. Es culpa del cliente, no del servidor, asi que la API
    /// la traduce a 400 y no deja escapar un 500 con stack trace.
    /// </summary>
    public class ComprobanteInvalidoException : Exception
    {
        public ComprobanteInvalidoException(string mensaje) : base(mensaje)
        {
        }

        public ComprobanteInvalidoException(string mensaje, Exception inner) : base(mensaje, inner)
        {
        }
    }
}
