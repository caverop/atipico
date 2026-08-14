using Atipico.Domain.Enums;
using Atipico.Domain.Interfaces;

namespace Atipico.Domain.Entities
{
    // Solo lectura: respaldada por la vista v_pedido_plato_sin_cobrar (sql/script_inicial.sql).
    // Platos servidos/pendientes de un pedido que aun no tienen DetalleCuenta.
    public class PedidoPlatoSinCobrar : IEntity
    {
        public long Id { get; set; } // id_pedido_plato
        public long IdPedido { get; set; }
        public string Plato { get; set; } = null!;
        public decimal Precio { get; set; }
        public EstadoPedidoPlato Estado { get; set; }
    }
}
