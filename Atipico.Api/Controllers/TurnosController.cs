using Atipico.Application.Interfaces.Services;
using Atipico.Application.Models;
using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atipico.Api.Controllers
{
    // No extiende EntityControllerBase<TurnoCaja> a proposito: abrir y cerrar no son un POST
    // y un PUT sobre un registro cualquiera, y DELETE no tiene sentido sobre un turno. Es un
    // controlador propio, como AuthController y ComprobantesController.
    // Ver docs/numero-pedido.md §7.2.
    [Route("api/turnos")]
    public class TurnosController : ApiControllerBase
    {
        // D-4: abrir y cerrar caja quedan en Admin y Cajero. El mesero no abre turno.
        private static readonly string[] RolesCaja = ["Admin", "Cajero"];

        // Un pedido "vivo" es el que impide cerrar: terminales son solo Cerrado y Anulado.
        // Servido cuenta como vivo y es el caso que justifica la regla — el plato ya salio,
        // la mesa esta comiendo, nadie pago. Mismo criterio que fn_turno_cierre.
        private static readonly EstadoPedido[] EstadosVivos =
            [EstadoPedido.Abierto, EstadoPedido.EnPreparacion, EstadoPedido.Servido];

        private readonly IEntityService<TurnoCaja> _turnos;
        private readonly IEntityService<Pedido> _pedidos;
        private readonly IEntityService<Usuario> _usuarios;

        public TurnosController(
            IEntityService<TurnoCaja> turnos,
            IEntityService<Pedido> pedidos,
            IEntityService<Usuario> usuarios)
        {
            _turnos = turnos;
            _pedidos = pedidos;
            _usuarios = usuarios;
        }

        /// <summary>
        /// El turno abierto, o 204 si no hay ninguno. 204 y no 404: "no hay turno abierto" es
        /// un estado normal del sistema —el que queda despues de la migracion y al terminar la
        /// jornada—, no un recurso que falta.
        /// </summary>
        [HttpGet("abierto")]
        public async Task<ActionResult<TurnoDto>> GetAbierto()
        {
            var turno = await BuscarAbiertoAsync();
            if (turno is null)
                return NoContent();

            return Ok(await ADtoAsync(turno));
        }

        /// <summary>
        /// Historico, del mas reciente al mas viejo. Lectura para cualquier rol autenticado,
        /// como el resto de los GET: la grilla ofrece el filtro de turno a Admin, Cajero y
        /// Cocinero, y restringirlo aca dejaria al cocinero sin poder poblarlo.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TurnoDto>>> GetAll()
        {
            var turnos = (await _turnos.GetAllAsync()).OrderByDescending(t => t.AbiertoEn).ToList();
            var nombres = await NombresDeCajeroAsync();

            // Una sola pasada por pedido para contar todos los turnos. Es la unica pantalla
            // que paga ese costo, y es la que casi no se abre: contar turno por turno serian
            // N consultas para una lista que se mira de vez en cuando.
            var porTurno = (await _pedidos.GetAllAsync())
                .GroupBy(p => p.IdTurnoCaja)
                .ToDictionary(g => g.Key, g => g.Count());

            return Ok(turnos.Select(t => new TurnoDto(
                t.Id, t.Nombre, t.AbiertoEn, t.CerradoEn,
                t.IdCajero is null ? null : nombres.GetValueOrDefault(t.IdCajero.Value),
                porTurno.GetValueOrDefault(t.Id))));
        }

        /// <summary>
        /// Abre un turno. Si hay uno vigente lo cierra primero, en la misma llamada: partirlo
        /// en cerrar-y-despues-abrir desde el cliente deja una ventana sin caja abierta en la
        /// que todo pedido que entre se rechaza (RN-6).
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<TurnoDto>> Abrir(AbrirTurnoRequest request)
        {
            if (!HasAnyRole(RolesCaja))
                return Forbid();

            var nombre = (request.Nombre ?? "").Trim();

            // Se valida ANTES de cerrar el vigente. ck_turno_caja_nombre lo rechazaria igual,
            // pero para entonces el turno anterior ya estaria cerrado y la caja quedaria sin
            // ninguno abierto por un nombre en blanco.
            if (nombre.Length == 0)
                return BadRequest(new { message = "El turno necesita un nombre." });

            if (nombre.Length > 40)
                return BadRequest(new { message = "El nombre del turno no puede superar los 40 caracteres." });

            var vigente = await BuscarAbiertoAsync();
            if (vigente is not null)
            {
                var bloqueo = await BloqueoDeCierreAsync(vigente);
                if (bloqueo is not null)
                    return Conflict(bloqueo);

                var cerrado = await CerrarAsync(vigente);
                if (cerrado is not null)
                    return cerrado;
            }

            var turno = new TurnoCaja
            {
                Nombre = nombre,
                // El cajero sale del token, nunca del cuerpo: si viniera del cliente,
                // cualquiera podria abrir un turno a nombre de otro (RN-7).
                IdCajero = UsuarioActualId,
            };

            try
            {
                await _turnos.AddAsync(turno);
            }
            catch (DbUpdateException ex)
            {
                var respuesta = TryTranslateDbError(ex);
                if (respuesta is not null)
                    return respuesta;
                throw;
            }

            return CreatedAtAction(nameof(GetAbierto), await ADtoAsync(turno));
        }

        /// <summary>Cierra el turno vigente y no abre ninguno.</summary>
        [HttpPost("cerrar")]
        public async Task<IActionResult> Cerrar()
        {
            if (!HasAnyRole(RolesCaja))
                return Forbid();

            var vigente = await BuscarAbiertoAsync();
            if (vigente is null)
                return Conflict(new { message = "No hay ningún turno de caja abierto." });

            var bloqueo = await BloqueoDeCierreAsync(vigente);
            if (bloqueo is not null)
                return Conflict(bloqueo);

            var error = await CerrarAsync(vigente);
            if (error is not null)
                return error;

            return NoContent();
        }

        // ---- Interno ---------------------------------------------------------------------

        private async Task<TurnoCaja?> BuscarAbiertoAsync() =>
            (await _turnos.FindAsync(t => t.CerradoEn == null)).FirstOrDefault();

        /// <summary>
        /// Los pedidos vivos que impiden cerrar, o null si no hay ninguno. Se adelanta al
        /// trigger a proposito: fn_turno_cierre solo puede decir cuantos son, y el cajero
        /// necesita saber cuales para ir a buscarlos.
        ///
        /// Las cuentas sin cobrar (§4.7.1) NO se validan aca: para saber cuales son hay que
        /// recorrer pedido -> pedido_plato -> detalle_cuenta -> cuenta, y reescribir ese
        /// recorrido en C# seria una segunda copia de la regla, condenada a separarse de la
        /// del trigger. Ese caso lo sigue rechazando fn_turno_cierre, y su mensaje llega
        /// como 409 legible por TryTranslateDbError (P0001).
        /// </summary>
        private async Task<CierreBloqueadoDto?> BloqueoDeCierreAsync(TurnoCaja turno)
        {
            var vivos = (await _pedidos.FindAsync(p =>
                    p.IdTurnoCaja == turno.Id && EstadosVivos.Contains(p.Estado)))
                .OrderBy(p => p.NumeroTurno)
                .ToList();

            if (vivos.Count == 0)
                return null;

            // Concuerda en numero: el texto sale tal cual en la pantalla del cajero.
            var mensaje = vivos.Count == 1
                ? "No se puede cerrar el turno: queda 1 pedido sin cerrar."
                : $"No se puede cerrar el turno: quedan {vivos.Count} pedidos sin cerrar.";

            return new CierreBloqueadoDto(
                mensaje,
                vivos.Select(p => new PedidoVivoDto(p.Id, p.NumeroTurno, p.Comensal, p.Estado)).ToList());
        }

        /// <summary>Devuelve null si cerro bien, o la respuesta de error ya traducida.</summary>
        private async Task<ActionResult?> CerrarAsync(TurnoCaja turno)
        {
            // La fecha nunca llega del cliente: el navegador arma el DateTimeOffset con su
            // propio huso y Npgsql solo acepta offset 0 para timestamptz.
            turno.CerradoEn = DateTimeOffset.UtcNow;

            try
            {
                await _turnos.UpdateAsync(turno);
                return null;
            }
            catch (DbUpdateException ex)
            {
                var respuesta = TryTranslateDbError(ex);
                if (respuesta is not null)
                    return respuesta;
                throw;
            }
        }

        private async Task<TurnoDto> ADtoAsync(TurnoCaja turno)
        {
            var pedidos = await _pedidos.FindAsync(p => p.IdTurnoCaja == turno.Id);

            string? cajero = null;
            if (turno.IdCajero is not null)
                cajero = (await _usuarios.GetByIdAsync(turno.IdCajero.Value))?.Nombre;

            return new TurnoDto(turno.Id, turno.Nombre, turno.AbiertoEn, turno.CerradoEn, cajero, pedidos.Count());
        }

        private async Task<Dictionary<long, string>> NombresDeCajeroAsync() =>
            (await _usuarios.GetAllAsync()).ToDictionary(u => u.Id, u => u.Nombre);
    }
}
