using Atipico.Domain.Entities;

namespace Atipico.Web.Services
{
    /// <summary>
    /// Mismo criterio que <see cref="EntityApiClientConProgreso{TEntity}"/>, para el cliente
    /// de comprobantes. Aquí la barra importa más que en el resto: la subida atraviesa el
    /// circuito SignalR, la API, el procesamiento de imagen y R2, así que es la espera más
    /// larga de la aplicación.
    /// </summary>
    public class ComprobanteApiClientConProgreso : IComprobanteApiClient
    {
        private readonly ComprobanteApiClient _interno;
        private readonly EstadoOperaciones _estado;

        public ComprobanteApiClientConProgreso(ComprobanteApiClient interno, EstadoOperaciones estado)
        {
            _interno = interno;
            _estado = estado;
        }

        public Task<List<ComprobantePago>> GetPorCuentaAsync(long idCuenta) =>
            _estado.SeguirAsync(() => _interno.GetPorCuentaAsync(idCuenta));

        public Task<string?> GetUrlAsync(long idComprobante) =>
            _estado.SeguirAsync(() => _interno.GetUrlAsync(idComprobante));

        public Task<ComprobantePago?> RegistrarAsync(
            long idCuenta,
            Stream contenido,
            string nombreArchivo,
            string tipoContenido,
            long? idReemplaza = null,
            string? motivoReemplazo = null) =>
            _estado.SeguirAsync(() => _interno.RegistrarAsync(
                idCuenta, contenido, nombreArchivo, tipoContenido, idReemplaza, motivoReemplazo));
    }
}
