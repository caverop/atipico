using Atipico.Domain.Enums;

namespace Atipico.Web
{
    public static class TipoPedidoExtensions
    {
        // El valor en base dice "salon" y la etiqueta dice "restaurante", a proposito:
        // tipo = 'EN_RESTAURANTE' dentro de la base de un restaurante no aporta nada, pero
        // "En el restaurante" es la palabra que usa el personal. La traduccion vive aca, en
        // un solo lugar, para que Pedidos/Index y Pedidos/Edit no se desincronicen.
        // Ver specs/tipo-pedido.md §3.1.
        public static string Etiqueta(this TipoPedido tipo) => tipo switch
        {
            TipoPedido.EnSalon => "En el restaurante",
            TipoPedido.ParaLlevar => "Para llevar",
            TipoPedido.Delivery => "Delivery",
            _ => tipo.ToString(),
        };
    }
}
