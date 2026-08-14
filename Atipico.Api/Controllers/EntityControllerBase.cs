using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Atipico.Api.Controllers
{
    [ApiController]
    [Authorize]
    public abstract class EntityControllerBase<TEntity> : ControllerBase where TEntity : class, IEntity
    {
        protected readonly IEntityService<TEntity> _service;

        protected EntityControllerBase(IEntityService<TEntity> service)
        {
            _service = service;
        }

        // Cualquier rol autenticado puede leer; solo estos roles pueden mutar.
        // Los controladores concretos sobrescriben lo que corresponda según el flujo de trabajo.
        protected virtual string[] CreateRoles => ["Admin"];
        protected virtual string[] UpdateRoles => ["Admin"];
        protected virtual string[] DeleteRoles => ["Admin"];

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TEntity>>> GetAll()
        {
            return Ok(await _service.GetAllAsync());
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<TEntity>> GetById(long id)
        {
            var entity = await _service.GetByIdAsync(id);
            return entity is null ? NotFound() : Ok(entity);
        }

        [HttpPost]
        public virtual async Task<ActionResult<TEntity>> Create(TEntity entity)
        {
            if (!HasAnyRole(CreateRoles))
                return Forbid();

            try
            {
                var created = await _service.AddAsync(entity);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (DbUpdateException ex)
            {
                var respuesta = TryTranslateDbError(ex);
                if (respuesta is not null)
                    return respuesta;
                throw;
            }
        }

        [HttpPut("{id:long}")]
        public virtual async Task<IActionResult> Update(long id, TEntity entity)
        {
            if (!HasAnyRole(UpdateRoles))
                return Forbid();

            if (id != entity.Id)
                return BadRequest("El id de la ruta no coincide con el id del cuerpo.");

            try
            {
                await _service.UpdateAsync(entity);
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

            return NoContent();
        }

        [HttpDelete("{id:long}")]
        public virtual async Task<IActionResult> Delete(long id)
        {
            if (!HasAnyRole(DeleteRoles))
                return Forbid();

            var entity = await _service.GetByIdAsync(id);
            if (entity is null)
                return NotFound();

            try
            {
                await _service.RemoveAsync(entity);
            }
            catch (DbUpdateException ex)
            {
                var respuesta = TryTranslateDbError(ex);
                if (respuesta is not null)
                    return respuesta;
                throw;
            }

            return NoContent();
        }

        protected bool HasAnyRole(string[] roles) => roles.Any(User.IsInRole);

        // Las reglas de negocio "duras" (unicidad, checks, FKs restrictivas, triggers de
        // inmutabilidad) viven en la base, no en la API — ver sql/script_inicial.sql. Sin esto,
        // cualquier violacion se propaga como 500 con stack trace crudo. Devuelve null si el
        // error no se reconoce, para que el llamador lo vuelva a lanzar tal cual.
        // Tipada como ActionResult (no IActionResult) para que sirva tanto en acciones que
        // devuelven IActionResult como en las que devuelven ActionResult<TEntity>.
        protected ActionResult? TryTranslateDbError(DbUpdateException ex)
        {
            if (ex.InnerException is not PostgresException pg)
                return null;

            return pg.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation =>
                    Conflict(new { message = DescribirRestriccion(pg.ConstraintName) ?? "Ya existe un registro con ese valor." }),
                PostgresErrorCodes.CheckViolation =>
                    BadRequest(new { message = DescribirRestriccion(pg.ConstraintName) ?? "El valor enviado no cumple una regla de negocio." }),
                PostgresErrorCodes.ForeignKeyViolation =>
                    Conflict(new { message = "No se puede completar la operación: hay registros relacionados que dependen de este." }),
                PostgresErrorCodes.InsufficientPrivilege =>
                    // La app se conecta como el rol app_restaurante (sql/script_inicial.sql), que
                    // no tiene GRANT DELETE a propósito: los registros se anulan, no se eliminan.
                    Conflict(new { message = "Esta operación no está permitida: los registros no se eliminan, se anulan." }),
                "P0001" =>
                    // Excepcion levantada a mano por un trigger (fn_cuenta_inmutable, fn_detalle_inmutable,
                    // fn_pedido_plato_facturado): el mensaje ya viene redactado en español para el usuario final.
                    Conflict(new { message = pg.MessageText }),
                _ => null
            };
        }

        private static string? DescribirRestriccion(string? constraintName) => constraintName switch
        {
            "uk_tipo_plato_nombre" => "Ya existe un tipo de plato con ese nombre.",
            "uk_mesa_numero" => "Ya existe una mesa con ese número.",
            "uk_usuario_nombre_usuario" => "Ya existe un usuario con ese nombre de usuario.",
            "uk_pedido_mesa" => "Esa mesa ya está asociada a este pedido.",
            "uk_detalle_pedido_plato" => "Ese plato del pedido ya fue facturado; no se puede facturar dos veces.",
            "ck_pedido_plato_servido" => "No se puede marcar como Servido sin la fecha de servido.",
            "ck_pedido_cierre" => "No se puede cerrar el pedido sin la fecha de cierre.",
            "ck_pedido_plato_anulacion" => "Para anular un plato se debe indicar responsable y motivo.",
            "ck_cuenta_pago" => "Para marcar la cuenta como Pagada se requiere método de pago, fecha de pago y cajero.",
            "ck_cuenta_anulacion" => "Para anular la cuenta se debe indicar responsable y motivo.",
            "ck_mesa_cap" => "La capacidad de la mesa debe ser mayor a cero.",
            "ck_plato_precio" => "El precio del plato no puede ser negativo.",
            "ck_cuenta_monto" => "El monto de la cuenta no puede ser negativo.",
            "ck_detalle_precio" => "El precio unitario no puede ser negativo.",
            _ => null
        };
    }
}
