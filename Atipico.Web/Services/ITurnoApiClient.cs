using Atipico.Application.Models;

namespace Atipico.Web.Services
{
    /// <summary>
    /// Todo lo que la interfaz necesita saber de los turnos de caja, incluida la grilla de
    /// pedidos acotada a uno. Ver docs/numero-pedido.md §7.
    ///
    /// No pasa por <see cref="IEntityApiClient{TEntity}"/> y no puede: ese contrato es el CRUD
    /// generico (GET/GET id/POST/PUT/DELETE sobre una entidad), y aca las operaciones son
    /// abrir y cerrar, que no son un POST y un PUT cualesquiera. Un DELETE sobre un turno no
    /// existe. Por lo mismo TurnoCaja NO se registra en ApiRoutes: hacerlo invitaria a pedir
    /// un IEntityApiClient&lt;TurnoCaja&gt; que devolveria 405 en la mitad de sus metodos.
    /// </summary>
    public interface ITurnoApiClient
    {
        /// <summary>Turno abierto y sus pedidos. Turno en null = no hay ninguno abierto.</summary>
        Task<GrillaPedidosDto> GetGrillaAbiertaAsync();

        /// <summary>Un turno cualquiera y sus pedidos. La API la niega a quien no puede ampliar.</summary>
        Task<GrillaPedidosDto> GetGrillaAsync(long idTurno);

        Task<List<TurnoDto>> GetHistoricoAsync();

        /// <summary>Cierra el vigente y abre uno nuevo, en una sola llamada.</summary>
        Task<TurnoDto?> AbrirAsync(string nombre);

        Task CerrarAsync();
    }
}
