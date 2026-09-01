using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Enums
{
    /// <summary>
    /// Donde se consume el pedido. Espejo de ck_pedido_tipo
    /// (sql/008_pedido_tipo.sql), en sync a mano.
    /// </summary>
    public enum TipoPedido
    {
        /// <summary>En el restaurante. Es el unico que espera mesa asociada.</summary>
        EnSalon,
        ParaLlevar,
        /// <summary>Por ahora solo una etiqueta: no hay direccion, telefono ni costo
        /// de envio. Ver specs/tipo-pedido.md §7.</summary>
        Delivery
    }
}
