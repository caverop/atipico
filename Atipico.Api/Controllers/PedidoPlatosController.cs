using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Atipico.Api.Controllers
{
    [Route("api/pedido-platos")]
    public class PedidoPlatosController : EntityControllerBase<PedidoPlato>
    {
        public PedidoPlatosController(IEntityService<PedidoPlato> service) : base(service)
        {
        }

        protected override string[] CreateRoles => ["Admin", "Mesero"];
        protected override string[] UpdateRoles => ["Admin", "Mesero", "Cocinero"];
        protected override string[] DeleteRoles => ["Admin", "Mesero"];
    }
}
