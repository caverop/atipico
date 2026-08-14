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
        private readonly IEntityService<PedidoPlatoSinCobrar> _sinCobrarService;
        private readonly IEntityService<PedidoMesa> _pedidoMesaService;
        private readonly IEntityService<Mesa> _mesaService;

        public PedidosController(
            IEntityService<Pedido> service,
            IEntityService<PedidoPlatoSinCobrar> sinCobrarService,
            IEntityService<PedidoMesa> pedidoMesaService,
            IEntityService<Mesa> mesaService)
            : base(service)
        {
            _sinCobrarService = sinCobrarService;
            _pedidoMesaService = pedidoMesaService;
            _mesaService = mesaService;
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
            var yaEstabaActivo = existing.Estado != EstadoPedido.Cerrado && existing.Estado != EstadoPedido.Anulado;
            var pasaAInactivo = yaEstabaActivo && (entity.Estado == EstadoPedido.Cerrado || entity.Estado == EstadoPedido.Anulado);

            // v_pedido_plato_sin_cobrar (sql/script_inicial.sql) esta pensada justo para esto:
            // "Cerrar un pedido es valido solo si no quedan filas suyas aqui".
            if (pasaACerrado)
            {
                var sinCobrar = (await _sinCobrarService.GetAllAsync()).Where(pp => pp.IdPedido == id).ToList();
                if (sinCobrar.Count > 0)
                {
                    var detalle = string.Join(", ", sinCobrar.Select(pp => pp.Plato));
                    return Conflict(new { message = $"No se puede cerrar el pedido: quedan {sinCobrar.Count} plato(s) sin facturar ({detalle})." });
                }
            }

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
            catch (DbUpdateException ex)
            {
                var respuesta = TryTranslateDbError(ex);
                if (respuesta is not null)
                    return respuesta;
                throw;
            }

            // mesa.estado no tiene trigger propio (ver sql/script_inicial.sql): al dejar de estar
            // activo el pedido, se liberan las mesas que ya no tienen ningun otro pedido activo.
            if (pasaAInactivo)
            {
                var idsMesa = (await _pedidoMesaService.GetAllAsync())
                    .Where(pm => pm.IdPedido == id)
                    .Select(pm => pm.IdMesa)
                    .Distinct();

                foreach (var idMesa in idsMesa)
                    await LiberarMesaSiNoTieneOtroPedidoActivoAsync(idMesa);
            }

            return NoContent();
        }

        private async Task LiberarMesaSiNoTieneOtroPedidoActivoAsync(long idMesa)
        {
            var mesa = await _mesaService.GetByIdAsync(idMesa);
            if (mesa is null || mesa.Estado != EstadoMesa.Ocupada)
                return;

            var idsPedidoConEstaMesa = (await _pedidoMesaService.GetAllAsync())
                .Where(pm => pm.IdMesa == idMesa)
                .Select(pm => pm.IdPedido)
                .ToHashSet();

            var tienePedidoActivo = (await _service.GetAllAsync())
                .Any(p => idsPedidoConEstaMesa.Contains(p.Id) && p.Estado != EstadoPedido.Cerrado && p.Estado != EstadoPedido.Anulado);

            if (!tienePedidoActivo)
            {
                mesa.Estado = EstadoMesa.Libre;
                await _mesaService.UpdateAsync(mesa);
            }
        }
    }
}
