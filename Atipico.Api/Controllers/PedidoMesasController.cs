using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atipico.Api.Controllers
{
    [Route("api/pedido-mesas")]
    public class PedidoMesasController : EntityControllerBase<PedidoMesa>
    {
        private readonly IEntityService<Mesa> _mesaService;
        private readonly IEntityService<Pedido> _pedidoService;

        public PedidoMesasController(IEntityService<PedidoMesa> service, IEntityService<Mesa> mesaService, IEntityService<Pedido> pedidoService)
            : base(service)
        {
            _mesaService = mesaService;
            _pedidoService = pedidoService;
        }

        protected override string[] CreateRoles => ["Admin", "Mesero"];
        protected override string[] UpdateRoles => ["Admin", "Mesero"];
        protected override string[] DeleteRoles => ["Admin", "Mesero"];

        // mesa.estado no tiene trigger propio en la base (ver sql/script_inicial.sql): se
        // transiciona aca, a mano, al asociar/desasociar una mesa de un pedido activo. Nunca
        // se toca una mesa Reservada/Inactiva: esos estados son decision manual del personal.
        public override async Task<ActionResult<PedidoMesa>> Create(PedidoMesa entity)
        {
            if (!HasAnyRole(CreateRoles))
                return Forbid();

            try
            {
                var created = await _service.AddAsync(entity);

                var mesa = await _mesaService.GetByIdAsync(created.IdMesa);
                if (mesa is not null && mesa.Estado == EstadoMesa.Libre)
                {
                    mesa.Estado = EstadoMesa.Ocupada;
                    await _mesaService.UpdateAsync(mesa);
                }

                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (DbUpdateException ex)
            {
                var respuesta = TryTranslateDbError(ex);
                if (respuesta is not null)
                    return respuesta;
                throw;
            }
        }

        public override async Task<IActionResult> Delete(long id)
        {
            if (!HasAnyRole(DeleteRoles))
                return Forbid();

            var entity = await _service.GetByIdAsync(id);
            if (entity is null)
                return NotFound();

            try
            {
                await _service.RemoveAsync(entity);
            }
            catch (DbUpdateException ex)
            {
                var respuesta = TryTranslateDbError(ex);
                if (respuesta is not null)
                    return respuesta;
                throw;
            }

            await LiberarMesaSiNoTieneOtroPedidoActivoAsync(entity.IdMesa);

            return NoContent();
        }

        private async Task LiberarMesaSiNoTieneOtroPedidoActivoAsync(long idMesa)
        {
            var mesa = await _mesaService.GetByIdAsync(idMesa);
            if (mesa is null || mesa.Estado != EstadoMesa.Ocupada)
                return;

            var idsPedidoConEstaMesa = (await _service.GetAllAsync())
                .Where(pm => pm.IdMesa == idMesa)
                .Select(pm => pm.IdPedido)
                .ToHashSet();

            var tienePedidoActivo = (await _pedidoService.GetAllAsync())
                .Any(p => idsPedidoConEstaMesa.Contains(p.Id) && p.Estado != EstadoPedido.Cerrado && p.Estado != EstadoPedido.Anulado);

            if (!tienePedidoActivo)
            {
                mesa.Estado = EstadoMesa.Libre;
                await _mesaService.UpdateAsync(mesa);
            }
        }
    }
}
