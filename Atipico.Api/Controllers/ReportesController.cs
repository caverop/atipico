using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atipico.Api.Controllers
{
    // Solo lectura sobre las vistas de sql/script_inicial.sql. No extiende EntityControllerBase<T>
    // a proposito: ese base expone Create/Update/Delete, que rompen sobre vistas sin PK real
    // (ver AppDbContext.PedidoPlatosSinCobrar/CuentasDescuadradas, mapeadas con HasNoKey()).
    [ApiController]
    [Authorize]
    [Route("api/reportes")]
    public class ReportesController : ControllerBase
    {
        private readonly IEntityService<PedidoPlatoSinCobrar> _pedidoPlatoSinCobrarService;
        private readonly IEntityService<CuentaDescuadrada> _cuentaDescuadradaService;

        public ReportesController(
            IEntityService<PedidoPlatoSinCobrar> pedidoPlatoSinCobrarService,
            IEntityService<CuentaDescuadrada> cuentaDescuadradaService)
        {
            _pedidoPlatoSinCobrarService = pedidoPlatoSinCobrarService;
            _cuentaDescuadradaService = cuentaDescuadradaService;
        }

        // Platos servidos/pendientes de cualquier pedido que aun no tienen DetalleCuenta.
        [HttpGet("pedido-platos-sin-cobrar")]
        public async Task<ActionResult<IEnumerable<PedidoPlatoSinCobrar>>> GetPedidoPlatosSinCobrar()
        {
            return Ok(await _pedidoPlatoSinCobrarService.GetAllAsync());
        }

        // Conciliacion: cuentas cuyo Monto no coincide con la suma de su detalle. En una base
        // sana, vacio. Reservado a roles con responsabilidad de caja/administracion.
        [HttpGet("cuentas-descuadradas")]
        [Authorize(Roles = "Admin,Cajero")]
        public async Task<ActionResult<IEnumerable<CuentaDescuadrada>>> GetCuentasDescuadradas()
        {
            return Ok(await _cuentaDescuadradaService.GetAllAsync());
        }
    }
}
