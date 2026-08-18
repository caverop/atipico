using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atipico.Api.Controllers
{
    [Route("api/pedidos")]
    public class PedidosController : EntityControllerBase<Pedido>
    {
        private readonly IEntityService<PedidoPlatoSinCobrar> _sinCobrarService;
        private readonly IEntityService<PedidoMesa> _pedidoMesaService;
        private readonly IEntityService<Mesa> _mesaService;
        private readonly IEntityService<PedidoPlato> _pedidoPlatoService;
        private readonly IEntityService<Plato> _platoService;
        private readonly IEntityService<Cuenta> _cuentaService;
        private readonly IEntityService<DetalleCuenta> _detalleCuentaService;

        public PedidosController(
            IEntityService<Pedido> service,
            IEntityService<PedidoPlatoSinCobrar> sinCobrarService,
            IEntityService<PedidoMesa> pedidoMesaService,
            IEntityService<Mesa> mesaService,
            IEntityService<PedidoPlato> pedidoPlatoService,
            IEntityService<Plato> platoService,
            IEntityService<Cuenta> cuentaService,
            IEntityService<DetalleCuenta> detalleCuentaService)
            : base(service)
        {
            _sinCobrarService = sinCobrarService;
            _pedidoMesaService = pedidoMesaService;
            _mesaService = mesaService;
            _pedidoPlatoService = pedidoPlatoService;
            _platoService = platoService;
            _cuentaService = cuentaService;
            _detalleCuentaService = detalleCuentaService;
        }

        protected override string[] CreateRoles => ["Admin", "Mesero"];
        protected override string[] UpdateRoles => ["Admin", "Mesero"];
        protected override string[] DeleteRoles => ["Admin", "Mesero"];

        // CerradoEn no llega del cliente: el navegador arma un DateTimeOffset con su propio
        // huso horario, y Npgsql solo acepta offset 0 (UTC) para timestamptz. Se calcula aca,
        // en el momento exacto del cierre, siempre en UTC.
        public override async Task<IActionResult> Update(long id, Pedido entity)
        {
            if (!HasAnyRole(UpdateRoles))
                return Forbid();

            if (id != entity.Id)
                return BadRequest("El id de la ruta no coincide con el id del cuerpo.");

            var existing = await _service.GetByIdAsync(id);
            if (existing is null)
                return NotFound();

            var pasaACerrado = entity.Estado == EstadoPedido.Cerrado && existing.Estado != EstadoPedido.Cerrado;
            var yaEstabaActivo = existing.Estado != EstadoPedido.Cerrado && existing.Estado != EstadoPedido.Anulado;
            var pasaAInactivo = yaEstabaActivo && (entity.Estado == EstadoPedido.Cerrado || entity.Estado == EstadoPedido.Anulado);
            var pasaAEnPreparacion = entity.Estado == EstadoPedido.EnPreparacion && existing.Estado == EstadoPedido.Abierto;

            // IEntityApiClient<T> es generico y lo usan 8+ paginas mas sin query params: en vez
            // de estirarlo, el metodo de pago viaja en la query string y se lee aca a mano. No
            // se declara como parametro de Update: es un override de
            // EntityControllerBase<TEntity>.Update(long, TEntity) y un tercer parametro rompe
            // esa relacion (CS0115) o registra una segunda accion HttpPut ambigua con la misma
            // ruta.
            MetodoPago? metodoPago = Enum.TryParse<MetodoPago>(Request.Query["metodoPago"].ToString(), ignoreCase: true, out var mp) ? mp : null;

            // v_pedido_plato_sin_cobrar (sql/script_inicial.sql) esta pensada justo para esto:
            // "Cerrar un pedido es valido solo si no quedan filas suyas aqui".
            if (pasaACerrado)
            {
                var sinCobrar = (await _sinCobrarService.GetAllAsync()).Where(pp => pp.IdPedido == id).ToList();
                if (sinCobrar.Count > 0)
                {
                    var detalle = string.Join(", ", sinCobrar.Select(pp => pp.Plato));
                    return Conflict(new { message = $"No se puede cerrar el pedido: quedan {sinCobrar.Count} plato(s) sin facturar ({detalle})." });
                }
            }

            // Al pasar a EnPreparacion se factura automaticamente (CrearCuentaAutomaticaAsync):
            // sin metodo de pago no hay con que crear la Cuenta. Ademas del boton deshabilitado
            // en Pedidos/Edit.razor (solo UI), se valida aca tambien.
            if (pasaAEnPreparacion && metodoPago is null)
                return BadRequest(new { message = "Selecciona un método de pago antes de pasar el pedido a En Preparación." });

            existing.Comensal = entity.Comensal;
            existing.Estado = entity.Estado;
            existing.IdMesero = entity.IdMesero;

            if (pasaACerrado)
                existing.CerradoEn = DateTimeOffset.UtcNow;

            try
            {
                await _service.UpdateAsync(existing);
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }
            catch (DbUpdateException ex)
            {
                var respuesta = TryTranslateDbError(ex);
                if (respuesta is not null)
                    return respuesta;
                throw;
            }

            // mesa.estado no tiene trigger propio (ver sql/script_inicial.sql): al dejar de estar
            // activo el pedido, se liberan las mesas que ya no tienen ningun otro pedido activo.
            if (pasaAInactivo)
            {
                var idsMesa = (await _pedidoMesaService.GetAllAsync())
                    .Where(pm => pm.IdPedido == id)
                    .Select(pm => pm.IdMesa)
                    .Distinct();

                foreach (var idMesa in idsMesa)
                    await LiberarMesaSiNoTieneOtroPedidoActivoAsync(idMesa);
            }

            // Facturacion automatica: mandar el pedido a cocina factura de una sola vez todo lo
            // pedido hasta ahora. fn_pedido_plato_facturado (sql/script_inicial.sql) hace que
            // este sea el unico momento en que estos PedidoPlato pueden facturarse: una vez con
            // DetalleCuenta, anularlos individualmente queda bloqueado para siempre (solo se
            // puede anular la Cuenta entera) — intencional, confirmado con el usuario, no un bug.
            if (pasaAEnPreparacion)
            {
                try
                {
                    await CrearCuentaAutomaticaAsync(existing, metodoPago!.Value);
                }
                catch (DbUpdateException ex)
                {
                    var respuesta = TryTranslateDbError(ex);
                    if (respuesta is not null)
                        return respuesta;
                    throw;
                }
            }

            return NoContent();
        }

        // Cuenta no tiene id_pedido (varias Cuentas por Pedido, ver sql/script_inicial.sql): se
        // crea una sola Cuenta para todo lo activo del pedido en este instante, con un
        // DetalleCuenta por PedidoPlato no Anulado. Si no queda ningun plato activo no se crea
        // nada.
        private async Task CrearCuentaAutomaticaAsync(Pedido pedido, MetodoPago metodoPago)
        {
            var platosActivos = (await _pedidoPlatoService.GetAllAsync())
                .Where(pp => pp.IdPedido == pedido.Id && pp.Estado != EstadoPedidoPlato.Anulado)
                .ToList();

            if (platosActivos.Count == 0)
                return;

            // Repository<TEntity> no hace Include (Repository.cs): PedidoPlato.Plato llega null,
            // hay que resolver el precio a mano, igual que Cuentas/Edit.razor con su
            // _platoPrecios.
            var precios = (await _platoService.GetAllAsync()).ToDictionary(p => p.Id, p => p.Precio);

            var cuenta = await _cuentaService.AddAsync(new Cuenta
            {
                Comensal = pedido.Comensal,
                IdMesero = pedido.IdMesero,
                MetodoPago = metodoPago,
                Monto = platosActivos.Sum(pp => precios.GetValueOrDefault(pp.IdPlato)),
            });

            foreach (var pp in platosActivos)
            {
                await _detalleCuentaService.AddAsync(new DetalleCuenta
                {
                    IdCuenta = cuenta.Id,
                    IdPedidoPlato = pp.Id,
                    PrecioUnitario = precios.GetValueOrDefault(pp.IdPlato),
                });
            }
        }

        private async Task LiberarMesaSiNoTieneOtroPedidoActivoAsync(long idMesa)
        {
            var mesa = await _mesaService.GetByIdAsync(idMesa);
            if (mesa is null || mesa.Estado != EstadoMesa.Ocupada)
                return;

            var idsPedidoConEstaMesa = (await _pedidoMesaService.GetAllAsync())
                .Where(pm => pm.IdMesa == idMesa)
                .Select(pm => pm.IdPedido)
                .ToHashSet();

            var tienePedidoActivo = (await _service.GetAllAsync())
                .Any(p => idsPedidoConEstaMesa.Contains(p.Id) && p.Estado != EstadoPedido.Cerrado && p.Estado != EstadoPedido.Anulado);

            if (!tienePedidoActivo)
            {
                mesa.Estado = EstadoMesa.Libre;
                await _mesaService.UpdateAsync(mesa);
            }
        }
    }
}
