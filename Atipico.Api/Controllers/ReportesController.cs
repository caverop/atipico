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
        private readonly IEntityService<CuentaQrEvidenciaIncompleta> _evidenciaIncompletaService;
        private readonly IEntityService<ComprobanteDuplicado> _comprobanteDuplicadoService;

        public ReportesController(
            IEntityService<PedidoPlatoSinCobrar> pedidoPlatoSinCobrarService,
            IEntityService<CuentaDescuadrada> cuentaDescuadradaService,
            IEntityService<CuentaQrEvidenciaIncompleta> evidenciaIncompletaService,
            IEntityService<ComprobanteDuplicado> comprobanteDuplicadoService)
        {
            _pedidoPlatoSinCobrarService = pedidoPlatoSinCobrarService;
            _cuentaDescuadradaService = cuentaDescuadradaService;
            _evidenciaIncompletaService = evidenciaIncompletaService;
            _comprobanteDuplicadoService = comprobanteDuplicadoService;
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

        // Cuentas QR cuya evidencia vigente no cubre el monto. Es la lista de trabajo del
        // cierre de turno, no un reporte de auditoria: el mesero la necesita para saber que
        // comprobante le falta adjuntar, asi que su rol tambien entra.
        [HttpGet("cuentas-qr-evidencia-incompleta")]
        [Authorize(Roles = "Admin,Cajero,Mesero")]
        public async Task<ActionResult<IEnumerable<CuentaQrEvidenciaIncompleta>>> GetEvidenciaIncompleta()
        {
            return Ok(await _evidenciaIncompletaService.GetAllAsync());
        }

        // Un mismo archivo registrado en cuentas distintas. A diferencia del anterior, esto
        // es un control sobre quien registra el cobro — y en el flujo de pago adelantado ese
        // es el mesero, asi que su rol queda fuera.
        [HttpGet("comprobantes-duplicados")]
        [Authorize(Roles = "Admin,Cajero")]
        public async Task<ActionResult<IEnumerable<ComprobanteDuplicado>>> GetComprobantesDuplicados()
        {
            return Ok(await _comprobanteDuplicadoService.GetAllAsync());
        }
    }
}
