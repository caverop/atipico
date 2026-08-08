using Atipico.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Entities
{
    public class DetalleCuenta : IEntity
    {
        public long Id { get; set; }
        public decimal PrecioUnitario { get; set; }

        public DateTimeOffset CreadoEn { get; set; }

        public long IdCuenta { get; set; }
        public Cuenta Cuenta { get; set; } = null!;

        public long IdPedidoPlato { get; set; }
        public PedidoPlato PedidoPlato { get; set; } = null!;
    }
}
