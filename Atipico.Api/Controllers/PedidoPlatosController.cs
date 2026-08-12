using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atipico.Api.Controllers
{
    [Route("api/pedido-platos")]
    public class PedidoPlatosController : EntityControllerBase<PedidoPlato>
    {
        public PedidoPlatosController(IEntityService<PedidoPlato> service) : base(service)
        {
        }

        protected override string[] CreateRoles => ["Admin", "Mesero"];
        protected override string[] UpdateRoles => ["Admin", "Mesero", "Cocinero"];
        protected override string[] DeleteRoles => ["Admin", "Mesero"];

        // ServidoEn/AnuladoEn no llegan del cliente: el navegador arma un DateTimeOffset
        // con su propio huso horario, y Npgsql solo acepta offset 0 (UTC) para timestamptz.
        // Se calculan aca, en el momento exacto de la transicion, siempre en UTC.
        public override async Task<IActionResult> Update(long id, PedidoPlato entity)
        {
            if (!HasAnyRole(UpdateRoles))
                return Forbid();

            if (id != entity.Id)
                return BadRequest("El id de la ruta no coincide con el id del cuerpo.");

            var existing = await _service.GetByIdAsync(id);
            if (existing is null)
                return NotFound();

            var pasaAServido = entity.Estado == EstadoPedidoPlato.Servido && existing.Estado != EstadoPedidoPlato.Servido;
            var pasaAAnulado = entity.Estado == EstadoPedidoPlato.Anulado && existing.Estado != EstadoPedidoPlato.Anulado;

            existing.IdPedido = entity.IdPedido;
            existing.IdPlato = entity.IdPlato;
            existing.Estado = entity.Estado;
            existing.IdAnuladoPor = entity.IdAnuladoPor;
            existing.MotivoAnulacion = entity.MotivoAnulacion;

            if (pasaAServido)
                existing.ServidoEn = DateTimeOffset.UtcNow;

            if (pasaAAnulado)
                existing.AnuladoEn = DateTimeOffset.UtcNow;

            try
            {
                await _service.UpdateAsync(existing);
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }
            catch (DbUpdateException ex)
            {
                var respuesta = TryTranslateDbError(ex);
                if (respuesta is not null)
                    return respuesta;
                throw;
            }

            return NoContent();
        }
    }
}
