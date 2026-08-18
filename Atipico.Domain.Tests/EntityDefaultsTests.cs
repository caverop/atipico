using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Domain.Interfaces;

namespace Atipico.Domain.Tests
{
    public class EntityDefaultsTests
    {
        [Fact]
        public void Usuario_Created_DefaultsToActivo()
        {
            var usuario = new Usuario();

            Assert.True(usuario.Activo);
            Assert.NotNull(usuario.PedidosAtendidos);
            Assert.Empty(usuario.PedidosAtendidos);
            Assert.NotNull(usuario.CuentasAtendidas);
            Assert.NotNull(usuario.CuentasCobradas);
            Assert.NotNull(usuario.CuentasAnuladas);
            Assert.NotNull(usuario.PlatosAnulados);
        }

        [Fact]
        public void TipoPlato_Created_HasEmptyPlatosCollection()
        {
            var tipoPlato = new TipoPlato();

            Assert.NotNull(tipoPlato.Platos);
            Assert.Empty(tipoPlato.Platos);
        }

        [Fact]
        public void Plato_Created_DefaultsToActivo()
        {
            var plato = new Plato();

            Assert.True(plato.Activo);
            Assert.Equal(EstadoPlato.Disponible, plato.Estado);
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), plato.HabilitadoDesde);
            Assert.Null(plato.HabilitadoHasta);
            Assert.NotNull(plato.PedidoPlatos);
            Assert.Empty(plato.PedidoPlatos);
        }

        [Fact]
        public void Mesa_Created_DefaultsToLibre()
        {
            var mesa = new Mesa();

            Assert.Equal(EstadoMesa.Libre, mesa.Estado);
            Assert.NotNull(mesa.PedidoMesas);
            Assert.Empty(mesa.PedidoMesas);
        }

        [Fact]
        public void Pedido_Created_DefaultsToAbierto()
        {
            var pedido = new Pedido();

            Assert.Equal(EstadoPedido.Abierto, pedido.Estado);
            Assert.Null(pedido.CerradoEn);
            Assert.NotNull(pedido.PedidoPlatos);
            Assert.NotNull(pedido.PedidoMesas);
        }

        [Fact]
        public void PedidoPlato_Created_DefaultsToPendiente()
        {
            var pedidoPlato = new PedidoPlato();

            Assert.Equal(EstadoPedidoPlato.Pendiente, pedidoPlato.Estado);
            Assert.Null(pedidoPlato.ServidoEn);
            Assert.Null(pedidoPlato.AnuladoEn);
        }

        [Fact]
        public void Cuenta_Created_DefaultsToAbierta()
        {
            var cuenta = new Cuenta();

            Assert.Equal(EstadoCuenta.Abierta, cuenta.Estado);
            Assert.Null(cuenta.MetodoPago);
            Assert.NotNull(cuenta.DetalleCuentas);
            Assert.Empty(cuenta.DetalleCuentas);
        }

        [Theory]
        [InlineData(typeof(Usuario))]
        [InlineData(typeof(TipoPlato))]
        [InlineData(typeof(Plato))]
        [InlineData(typeof(Mesa))]
        [InlineData(typeof(Pedido))]
        [InlineData(typeof(PedidoMesa))]
        [InlineData(typeof(PedidoPlato))]
        [InlineData(typeof(Cuenta))]
        [InlineData(typeof(DetalleCuenta))]
        public void Entity_ImplementsIEntity(Type entityType)
        {
            Assert.True(typeof(IEntity).IsAssignableFrom(entityType), $"{entityType.Name} debe implementar IEntity.");
        }
    }
}
