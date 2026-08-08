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
    }
}
