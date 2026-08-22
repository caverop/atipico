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

        // Entrega. Solo se usan cuando Tipo es Delivery; las cuatro son anulables porque un
        // pedido puede nacer sin ellas. Ver docs/direccion-entrega.md.

        /// <summary>La referencia escrita: "casa verde, media cuadra del surtidor".</summary>
        public string? DireccionEntrega { get; set; }

        /// <summary>Lo que llegó de WhatsApp tal cual, se haya podido parsear o no.</summary>
        public string? UbicacionCompartida { get; set; }

        /// <summary>ck_pedido_coordenada: va junto con <see cref="LongitudEntrega"/> o no va.</summary>
        public decimal? LatitudEntrega { get; set; }

        public decimal? LongitudEntrega { get; set; }

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
