using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Entities
{
    public class PedidoMesa
    {
        public long Id { get; set; }

        public DateTimeOffset CreadoEn { get; set; }

        public long IdPedido { get; set; }
        public Pedido Pedido { get; set; } = null!;

        public long IdMesa { get; set; }
        public Mesa Mesa { get; set; } = null!;
    }
}
