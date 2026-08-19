using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;

namespace Atipico.Api.Controllers
{
    /// <summary>
    /// Lo que necesita cualquier controlador que escriba en la base, extienda o no
    /// <see cref="EntityControllerBase{TEntity}"/>. Se extrajo aca cuando
    /// ComprobantesController —que no es CRUD sobre una entidad y por eso no hereda del
    /// base generico— tambien necesito traducir los errores de la base: duplicar el mapeo
    /// de restricciones habria garantizado que las dos copias se separaran con el tiempo.
    /// </summary>
    [ApiController]
    [Authorize]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected bool HasAnyRole(string[] roles) => roles.Any(User.IsInRole);

        /// <summary>
        /// Id del usuario autenticado, tomado del token. Null si el claim no esta o no es
        /// un numero, lo que en la practica solo pasa con un token manipulado.
        /// </summary>
        protected long? UsuarioActualId =>
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

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
                    // fn_pedido_plato_facturado, fn_comprobante_inmutable): el mensaje ya viene
                    // redactado en español para el usuario final.
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
            "uk_pedido_comensal_activo" => "Ya hay un pedido abierto o en preparación para ese comensal.",
            "uk_detalle_pedido_plato" => "Ese plato del pedido ya fue facturado; no se puede facturar dos veces.",
            "ck_pedido_plato_servido" => "No se puede marcar como Servido sin la fecha de servido.",
            "ck_pedido_cierre" => "No se puede cerrar el pedido sin la fecha de cierre.",
            "ck_pedido_plato_anulacion" => "Para anular un plato se debe indicar responsable y motivo.",
            "ck_cuenta_pago" => "Para marcar la cuenta como Pagada se requiere método de pago, fecha de pago y cajero.",
            "ck_cuenta_anulacion" => "Para anular la cuenta se debe indicar responsable y motivo.",
            "ck_mesa_cap" => "La capacidad de la mesa debe ser mayor a cero.",
            "ck_plato_precio" => "El precio del plato no puede ser negativo.",
            "ck_plato_estado" => "El estado del plato no es válido.",
            "ck_plato_habilitado_rango" => "La fecha 'habilitado hasta' no puede ser anterior a 'habilitado desde'.",
            "ck_cuenta_monto" => "El monto de la cuenta no puede ser negativo.",
            "ck_detalle_precio" => "El precio unitario no puede ser negativo.",
            // Comprobantes de pago QR (sql/006_comprobante_pago.sql).
            "uk_comprobante_cuenta_hash" => "Ese mismo comprobante ya está registrado en esta cuenta.",
            "uk_comprobante_reemplaza" => "Ese comprobante ya fue reemplazado por otro.",
            "uk_comprobante_key" => "Ya existe un comprobante guardado con esa clave.",
            "ck_comprobante_monto" => "El monto del comprobante debe ser mayor a cero.",
            "ck_comprobante_bytes" => "El archivo del comprobante está vacío.",
            "ck_comprobante_tipo" => "El formato de imagen del comprobante no está permitido.",
            "ck_comprobante_reemplazo" => "Para reemplazar un comprobante se debe indicar el motivo.",
            _ => null
        };
    }
}
