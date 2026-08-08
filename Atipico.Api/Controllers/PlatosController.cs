using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Atipico.Api.Controllers
{
    [Route("api/platos")]
    public class PlatosController : EntityControllerBase<Plato>
    {
        public PlatosController(IEntityService<Plato> service) : base(service)
        {
        }
    }
}
