using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Atipico.Api.Controllers
{
    [Route("api/tipos-plato")]
    public class TipoPlatosController : EntityControllerBase<TipoPlato>
    {
        public TipoPlatosController(IEntityService<TipoPlato> service) : base(service)
        {
        }
    }
}
