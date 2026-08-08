using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Atipico.Api.Controllers
{
    [Route("api/usuarios")]
    public class UsuariosController : EntityControllerBase<Usuario>
    {
        public UsuariosController(IEntityService<Usuario> service) : base(service)
        {
        }
    }
}
