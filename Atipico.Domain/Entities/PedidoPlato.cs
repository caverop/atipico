using Atipico.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Entities
{
    public class PedidoPlato
    {
        public long Id { get; set; }
        public EstadoPedidoPlato Estado { get; set; } = EstadoPedidoPlato.Pendiente;

        public DateTimeOffset CreadoEn { get; set; }

        /// <summary>ck_pedido_plato_servido lo exige cuando Estado es Servido.</summary>
        public DateTimeOffset? ServidoEn { get; set; }

        public DateTimeOffset? AnuladoEn { get; set; }
        public long? IdAnuladoPor { get; set; }
        public Usuario? AnuladoPor { get; set; }
        public string? MotivoAnulacion { get; set; }

        public long IdPedido { get; set; }
        public Pedido Pedido { get; set; } = null!;

        public long IdPlato { get; set; }
        public Plato Plato { get; set; } = null!;

        /// <summary>Cada plato se factura como máximo una vez (uk_detalle_pedido_plato).</summary>
        public DetalleCuenta? DetalleCuenta { get; set; }
    }
}
