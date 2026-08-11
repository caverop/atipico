using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Atipico.Api.Controllers
{
    [Route("api/detalle-cuentas")]
    public class DetalleCuentasController : EntityControllerBase<DetalleCuenta>
    {
        public DetalleCuentasController(IEntityService<DetalleCuenta> service) : base(service)
        {
        }

        protected override string[] CreateRoles => ["Admin", "Cajero"];
        protected override string[] UpdateRoles => ["Admin", "Cajero"];
    }
}
