using Atipico.Application.Models;
using Atipico.Domain.Entities;

namespace Atipico.Application.Interfaces.Services
{
    public interface IComprobanteService
    {
        Task<ComprobantePago> RegistrarAsync(RegistrarComprobante solicitud, CancellationToken ct = default);

        /// <summary>Historial completo de una cuenta, incluidos los reemplazados, mas reciente primero.</summary>
        Task<IEnumerable<ComprobantePago>> ObtenerPorCuentaAsync(long idCuenta);

        /// <summary>Null si el comprobante no existe.</summary>
        Task<string?> GenerarUrlAsync(long idComprobante, TimeSpan duracion);
    }
}
