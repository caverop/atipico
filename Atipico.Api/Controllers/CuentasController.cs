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
        // En el flujo de pago adelantado por QR el mesero tampoco necesita Update: crea la
        // cuenta ya en su estado final (PAGADA) y no la vuelve a tocar, que es justo lo que
        // permite fn_cuenta_inmutable.
        protected override string[] CreateRoles => ["Admin", "Mesero", "Cajero"];
        protected override string[] UpdateRoles => ["Admin", "Cajero"];

        // Pago adelantado: el comensal paga antes de ser atendido, asi que la cuenta nace
        // PAGADA en vez de pasar por ABIERTA. ck_cuenta_pago exige entonces metodo, momento
        // y cajero. El momento se sella aca en UTC por lo mismo que en Update, y el cajero
        // es el propio mesero cuando no viene otro: en este flujo cobra quien atiende.
        public override async Task<ActionResult<Cuenta>> Create(Cuenta entity)
        {
            if (!HasAnyRole(CreateRoles))
                return Forbid();

            if (entity.Estado == EstadoCuenta.Pagada)
            {
                entity.PagadoEn ??= DateTimeOffset.UtcNow;
                entity.IdCajero ??= entity.IdMesero;
            }

            return await base.Create(entity);
        }

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
