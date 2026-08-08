using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Atipico.Api.Controllers
{
    [Route("api/pedidos")]
    public class PedidosController : EntityControllerBase<Pedido>
    {
        public PedidosController(IEntityService<Pedido> service) : base(service)
        {
        }
    }
}
