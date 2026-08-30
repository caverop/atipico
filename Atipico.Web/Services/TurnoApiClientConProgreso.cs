using Atipico.Application.Models;

namespace Atipico.Web.Services
{
    /// <summary>
    /// Mismo criterio que <see cref="EntityApiClientConProgreso{TEntity}"/>: el decorador
    /// reporta a <see cref="EstadoOperaciones"/> para que la barra se encienda sola. Sin esto,
    /// toda la pantalla de pedidos —que ahora carga por esta via— quedaria sin barra.
    /// </summary>
    public class TurnoApiClientConProgreso : ITurnoApiClient
    {
        private readonly TurnoApiClient _interno;
        private readonly EstadoOperaciones _estado;

        public TurnoApiClientConProgreso(TurnoApiClient interno, EstadoOperaciones estado)
        {
            _interno = interno;
            _estado = estado;
        }

        public Task<GrillaPedidosDto> GetGrillaAbiertaAsync() =>
            _estado.SeguirAsync(() => _interno.GetGrillaAbiertaAsync());

        public Task<GrillaPedidosDto> GetGrillaAsync(long idTurno) =>
            _estado.SeguirAsync(() => _interno.GetGrillaAsync(idTurno));

        public Task<List<TurnoDto>> GetHistoricoAsync() =>
            _estado.SeguirAsync(() => _interno.GetHistoricoAsync());

        public Task<TurnoDto?> AbrirAsync(string nombre) =>
            _estado.SeguirAsync(() => _interno.AbrirAsync(nombre));

        public Task CerrarAsync() =>
            _estado.SeguirAsync(() => _interno.CerrarAsync());
    }
}
