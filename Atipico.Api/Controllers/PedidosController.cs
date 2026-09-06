using Atipico.Application.Interfaces.Services;
using Atipico.Application.Models;
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
        private readonly IEntityService<TurnoCaja> _turnoService;
        private readonly IEntityService<Usuario> _usuarioService;

        public PedidosController(
            IEntityService<Pedido> service,
            IEntityService<PedidoPlatoSinCobrar> sinCobrarService,
            IEntityService<PedidoMesa> pedidoMesaService,
            IEntityService<Mesa> mesaService,
            IEntityService<PedidoPlato> pedidoPlatoService,
            IEntityService<Plato> platoService,
            IEntityService<Cuenta> cuentaService,
            IEntityService<DetalleCuenta> detalleCuentaService,
            IEntityService<TurnoCaja> turnoService,
            IEntityService<Usuario> usuarioService)
            : base(service)
        {
            _sinCobrarService = sinCobrarService;
            _pedidoMesaService = pedidoMesaService;
            _mesaService = mesaService;
            _pedidoPlatoService = pedidoPlatoService;
            _platoService = platoService;
            _cuentaService = cuentaService;
            _detalleCuentaService = detalleCuentaService;
            _turnoService = turnoService;
            _usuarioService = usuarioService;
        }

        protected override string[] CreateRoles => ["Admin", "Mesero"];
        protected override string[] UpdateRoles => ["Admin", "Mesero"];
        protected override string[] DeleteRoles => ["Admin", "Mesero"];

        // Quien puede mirar un turno que no es el abierto. El mesero no esta, y no es un
        // detalle de maquetado: para el que canta "el 12" en el salon, el numero tiene que
        // ser inequivoco, y deja de serlo apenas la lista abarca dos turnos (RN-12).
        // La grilla ya se lo esconde; esto lo hace cierto tambien si alguien llama a la ruta
        // a mano.
        private static readonly string[] RolesQueAmplian = ["Admin", "Cajero", "Cocinero"];

        // Delivery es el unico rol con una restriccion de LECTURA (SCRUM-13): solo ve los
        // pedidos listos para repartir. Es la UNICA restriccion de este rol — no tiene
        // CreateRoles, UpdateRoles ni DeleteRoles propios en ningun controlador, asi que para
        // todo lo demas (que hoy es "nada": no puede escribir en ninguna entidad) se comporta
        // como cualquier usuario autenticado sin rol elevado.
        //
        // Se escribe como "el usuario no tiene ningun rol mas amplio" y no como "el usuario ES
        // Delivery", por el mismo motivo que ya separa RolesQueAmplian de una comparacion
        // directa: el corte es por capacidad, no por identidad (ver
        // GetDelTurno_MeseroQueTambienEsAdmin_Pasa). Hoy Usuario.Rol es un unico valor por
        // usuario (JwtTokenGenerator emite un solo Claim de rol), asi que en la practica un
        // usuario Delivery nunca tiene ademas Admin/Cajero/Cocinero/Mesero — pero el codigo no
        // depende de esa invariante para ser correcto.
        private static readonly string[] RolesSinRestriccionDePedidos =
            ["Admin", "Cajero", "Cocinero", "Mesero"];

        private IEnumerable<Pedido> AplicarRestriccionDelivery(IEnumerable<Pedido> pedidos) =>
            HasAnyRole(RolesSinRestriccionDePedidos)
                ? pedidos
                : pedidos.Where(p => p.Estado == EstadoPedido.Servido && p.Tipo == TipoPedido.Delivery);

        // GetAll/GetById: defensa en profundidad. La UI de Delivery nunca navega a
        // Pedidos/Edit.razor, pero alguien que llame a la API directamente con un token de
        // Delivery no deberia poder leer un pedido fuera de su vista.
        public override async Task<ActionResult<IEnumerable<Pedido>>> GetAll() =>
            Ok(AplicarRestriccionDelivery(await _service.GetAllAsync()));

        public override async Task<ActionResult<Pedido>> GetById(long id)
        {
            var entity = await _service.GetByIdAsync(id);
            if (entity is null)
                return NotFound();

            // 404 y no 403: para Delivery, un pedido que no cumple la restriccion no existe en
            // su vista, igual que ArmarGrillaAsync ya lo excluye de la lista en vez de listarlo
            // con un candado. Un 403 confirmaria que el id corresponde a un pedido real; un 404
            // no revela nada.
            return AplicarRestriccionDelivery([entity]).Any() ? Ok(entity) : NotFound();
        }

        /// <summary>
        /// Los pedidos del turno abierto, con el turno en el sobre. Reemplaza al GET api/pedidos
        /// de la grilla, que traia todos los pedidos que existieron para descartarlos en
        /// memoria. Ver specs/numero-pedido.md §7.3.
        /// </summary>
        [HttpGet("turno-abierto")]
        public async Task<ActionResult<GrillaPedidosDto>> GetDelTurnoAbierto()
        {
            var turno = (await _turnoService.FindAsync(t => t.CerradoEn == null)).FirstOrDefault();

            // Sin turno abierto no hay grilla que mostrar, y tampoco es un error: es el estado
            // en que queda la base recien migrada y al terminar la jornada. El sobre lo dice
            // con Turno en null y la pantalla ofrece abrir uno (§8.4).
            if (turno is null)
                return Ok(new GrillaPedidosDto(null, [], new Dictionary<long, IReadOnlyList<int>>()));

            return Ok(await ArmarGrillaAsync(turno));
        }

        /// <summary>Los pedidos de un turno cualquiera. Solo para los roles que pueden ampliar.</summary>
        [HttpGet("turno/{idTurno:long}")]
        public async Task<ActionResult<GrillaPedidosDto>> GetDelTurno(long idTurno)
        {
            if (!HasAnyRole(RolesQueAmplian))
                return Forbid();

            var turno = await _turnoService.GetByIdAsync(idTurno);
            if (turno is null)
                return NotFound();

            return Ok(await ArmarGrillaAsync(turno));
        }

        private async Task<GrillaPedidosDto> ArmarGrillaAsync(TurnoCaja turno)
        {
            var pedidos = AplicarRestriccionDelivery(
                await _service.FindAsync(p => p.IdTurnoCaja == turno.Id)).ToList();

            string? cajero = null;
            if (turno.IdCajero is not null)
                cajero = (await _usuarioService.GetByIdAsync(turno.IdCajero.Value))?.Nombre;

            var mesasPorPedido = await MesasPorPedidoAsync(pedidos.Select(p => p.Id));

            // TotalPedidos sale de la lista que ya se trajo, no de un COUNT aparte.
            return new GrillaPedidosDto(
                new TurnoDto(turno.Id, turno.Nombre, turno.AbiertoEn, turno.CerradoEn, cajero, pedidos.Count),
                pedidos,
                mesasPorPedido);
        }

        // Una sola pasada por pedido_mesa, filtrada por los pedidos del turno (no toda la
        // tabla: FindAsync se traduce a SQL, a diferencia de traer GetAllAsync y filtrar en
        // memoria). Mesa si se trae entera - es una tabla chica que no crece con el tiempo,
        // igual criterio que ya usa Pedidos/Edit.razor con MesaApi.GetAllAsync().
        private async Task<IReadOnlyDictionary<long, IReadOnlyList<int>>> MesasPorPedidoAsync(IEnumerable<long> idsPedido)
        {
            var idsPedidoSet = idsPedido.ToHashSet();
            if (idsPedidoSet.Count == 0)
                return new Dictionary<long, IReadOnlyList<int>>();

            var pedidoMesas = await _pedidoMesaService.FindAsync(pm => idsPedidoSet.Contains(pm.IdPedido));
            var numerosMesa = (await _mesaService.GetAllAsync()).ToDictionary(m => m.Id, m => m.Numero);

            return pedidoMesas
                .GroupBy(pm => pm.IdPedido)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<int>)g.Select(pm => numerosMesa.GetValueOrDefault(pm.IdMesa))
                                              .OrderBy(n => n)
                                              .ToList());
        }

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

            // Update no vuelca la entidad recibida: carga la fila y copia campo por campo.
            // Toda columna que falte aca es una columna que ningun PUT puede modificar nunca.
            existing.Comensal = entity.Comensal;
            existing.Estado = entity.Estado;
            existing.Tipo = entity.Tipo;
            existing.IdMesero = entity.IdMesero;
            existing.DireccionEntrega = entity.DireccionEntrega;
            existing.UbicacionCompartida = entity.UbicacionCompartida;
            existing.LatitudEntrega = entity.LatitudEntrega;
            existing.LongitudEntrega = entity.LongitudEntrega;

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
