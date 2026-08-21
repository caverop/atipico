using Atipico.Domain.Enums;
using Atipico.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Entities
{
    public class Pedido : IEntity
    {
        public long Id { get; set; }
        public string? Comensal { get; set; }
        public EstadoPedido Estado { get; set; } = EstadoPedido.Abierto;

        /// <summary>Donde se consume. ck_pedido_tipo. Ver docs/tipo-pedido.md.</summary>
        public TipoPedido Tipo { get; set; } = TipoPedido.EnSalon;

        public DateTimeOffset CreadoEn { get; set; }
        public DateTimeOffset ActualizadoEn { get; set; }

        /// <summary>ck_pedido_cierre lo exige cuando Estado es Cerrado.</summary>
        public DateTimeOffset? CerradoEn { get; set; }

        public long IdMesero { get; set; }
        public Usuario Mesero { get; set; } = null!;

        public ICollection<PedidoPlato> PedidoPlatos { get; set; } = new List<PedidoPlato>();
        public ICollection<PedidoMesa> PedidoMesas { get; set; } = new List<PedidoMesa>();
    }
}
