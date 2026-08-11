using Atipico.Domain.Entities;

namespace Atipico.Web.Services
{
    public static class ApiRoutes
    {
        private static readonly Dictionary<Type, string> Routes = new()
        {
            [typeof(Usuario)] = "usuarios",
            [typeof(TipoPlato)] = "tipos-plato",
            [typeof(Plato)] = "platos",
            [typeof(Mesa)] = "mesas",
            [typeof(Pedido)] = "pedidos",
            [typeof(PedidoMesa)] = "pedido-mesas",
            [typeof(PedidoPlato)] = "pedido-platos",
            [typeof(Cuenta)] = "cuentas",
            [typeof(DetalleCuenta)] = "detalle-cuentas",
        };

        public static string For<TEntity>()
        {
            if (!Routes.TryGetValue(typeof(TEntity), out var route))
                throw new InvalidOperationException($"No hay ruta de API registrada para {typeof(TEntity).Name}.");

            return route;
        }
    }
}
