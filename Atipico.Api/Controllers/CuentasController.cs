using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atipico.Api.Controllers
{
    [Route("api/cuentas")]
    public class CuentasController : EntityControllerBase<Cuenta>
    {
        public CuentasController(IEntityService<Cuenta> service) : base(service)
        {
        }

        // Mesero abre la cuenta pero no la cobra: cobrar (Update) es cosa de Cajero/Admin.
        protected override string[] CreateRoles => ["Admin", "Mesero", "Cajero"];
        protected override string[] UpdateRoles => ["Admin", "Cajero"];

        // PagadoEn/AnuladoEn no llegan del cliente: el navegador arma un DateTimeOffset con su
        // propio huso horario, y Npgsql solo acepta offset 0 (UTC) para timestamptz. Se calculan
        // aca, en el momento exacto de la transicion, siempre en UTC.
        public override async Task<IActionResult> Update(long id, Cuenta entity)
        {
            if (!HasAnyRole(UpdateRoles))
                return Forbid();

            if (id != entity.Id)
                return BadRequest("El id de la ruta no coincide con el id del cuerpo.");

            var existing = await _service.GetByIdAsync(id);
            if (existing is null)
                return NotFound();

            var pasaAPagada = entity.Estado == EstadoCuenta.Pagada && existing.Estado != EstadoCuenta.Pagada;
            var pasaAAnulada = entity.Estado == EstadoCuenta.Anulada && existing.Estado != EstadoCuenta.Anulada;

            existing.Comensal = entity.Comensal;
            existing.Estado = entity.Estado;
            existing.MetodoPago = entity.MetodoPago;
            existing.Monto = entity.Monto;
            existing.IdMesero = entity.IdMesero;
            existing.IdCajero = entity.IdCajero;
            existing.IdAnuladoPor = entity.IdAnuladoPor;
            existing.MotivoAnulacion = entity.MotivoAnulacion;

            if (pasaAPagada)
                existing.PagadoEn = DateTimeOffset.UtcNow;

            if (pasaAAnulada)
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
