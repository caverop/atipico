using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Atipico.Api.Controllers
{
    [Route("api/pedido-mesas")]
    public class PedidoMesasController : EntityControllerBase<PedidoMesa>
    {
        public PedidoMesasController(IEntityService<PedidoMesa> service) : base(service)
        {
        }

        protected override string[] CreateRoles => ["Admin", "Mesero"];
        protected override string[] UpdateRoles => ["Admin", "Mesero"];
        protected override string[] DeleteRoles => ["Admin", "Mesero"];
    }
}
