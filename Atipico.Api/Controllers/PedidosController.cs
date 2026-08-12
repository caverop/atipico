using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atipico.Api.Controllers
{
    [Route("api/pedidos")]
    public class PedidosController : EntityControllerBase<Pedido>
    {
        public PedidosController(IEntityService<Pedido> service) : base(service)
        {
        }

        protected override string[] CreateRoles => ["Admin", "Mesero"];
        protected override string[] UpdateRoles => ["Admin", "Mesero"];
        protected override string[] DeleteRoles => ["Admin", "Mesero"];

        // CerradoEn no llega del cliente: el navegador arma un DateTimeOffset con su propio
        // huso horario, y Npgsql solo acepta offset 0 (UTC) para timestamptz. Se calcula aca,
        // en el momento exacto del cierre, siempre en UTC.
        public override async Task<IActionResult> Update(long id, Pedido entity)
        {
            if (!HasAnyRole(UpdateRoles))
                return Forbid();

            if (id != entity.Id)
                return BadRequest("El id de la ruta no coincide con el id del cuerpo.");

            var existing = await _service.GetByIdAsync(id);
            if (existing is null)
                return NotFound();

            var pasaACerrado = entity.Estado == EstadoPedido.Cerrado && existing.Estado != EstadoPedido.Cerrado;

            existing.Comensal = entity.Comensal;
            existing.Estado = entity.Estado;
            existing.IdMesero = entity.IdMesero;

            if (pasaACerrado)
                existing.CerradoEn = DateTimeOffset.UtcNow;

            try
            {
                await _service.UpdateAsync(existing);
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
