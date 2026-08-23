using Atipico.Domain.Enums;

namespace Atipico.Web
{
    public static class EstadoPedidoExtensions
    {
        // Mismo criterio que TipoPedidoExtensions: el nombre del enum es para el codigo,
        // no para la pantalla. "EnPreparacion" pegado se lee mal en una pastilla de
        // estado, que es justo donde mas se mira.
        public static string Etiqueta(this EstadoPedido estado) => estado switch
        {
            EstadoPedido.EnPreparacion => "En preparación",
            _ => estado.ToString(),
        };

        /// <summary>
        /// Modificador visual de <c>.estado-pill</c>. El estado en curso va lleno de
        /// tinta, el servido en oliva (lo que ya se puede cobrar) y los terminales
        /// apagados. Sin colores fuera de la paleta.
        /// </summary>
        public static string ClasePill(this EstadoPedido estado) => estado switch
        {
            EstadoPedido.Abierto => "",
            EstadoPedido.EnPreparacion => "llena",
            EstadoPedido.Servido => "acento",
            _ => "apagada",
        };
    }
}
