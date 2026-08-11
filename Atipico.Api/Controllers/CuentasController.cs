using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

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
    }
}
