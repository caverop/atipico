using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Atipico.Api.Controllers
{
    [Route("api/mesas")]
    public class MesasController : EntityControllerBase<Mesa>
    {
        public MesasController(IEntityService<Mesa> service) : base(service)
        {
        }
    }
}
