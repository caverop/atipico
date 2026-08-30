using Atipico.Application.Models;

namespace Atipico.Web.Services
{
    /// <summary>
    /// Un cierre de turno rechazado porque quedan pedidos vivos, con la lista de cuales son.
    /// Es una <see cref="ApiException"/> para que cualquier pantalla que ya capture esa siga
    /// mostrando el mensaje sin cambios; las que quieran mostrar el detalle capturan esta
    /// primero.
    /// </summary>
    public class CierreTurnoException : ApiException
    {
        public CierreTurnoException(string message, IReadOnlyList<PedidoVivoDto> pedidos)
            : base(message)
        {
            Pedidos = pedidos;
        }

        public IReadOnlyList<PedidoVivoDto> Pedidos { get; }
    }
}
