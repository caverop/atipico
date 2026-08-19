using Atipico.Application.Common.Exceptions;
using Atipico.Application.Interfaces.Services;
using Atipico.Application.Models;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atipico.Api.Controllers
{
    // No extiende EntityControllerBase<T> a proposito: ese base expone un CRUD JSON, y
    // aca la entrada es multipart/form-data con un archivo, mientras que Update y Delete
    // no existen — tg_comprobante_inmutable los rechaza. Un comprobante se corrige
    // registrando otro que apunte al anterior, nunca editandolo.
    [Route("api/comprobantes")]
    public class ComprobantesController : ApiControllerBase
    {
        // El mesero tambien: en el flujo de pago adelantado es quien verifica el
        // comprobante en el mostrador y quien lo adjunta despues.
        private static readonly string[] RolesComprobante = ["Admin", "Cajero", "Mesero"];

        // Suficiente para abrir la imagen y cerrar la pestaña; corto para que la URL
        // firmada no sirva de enlace permanente si se comparte por accidente.
        private static readonly TimeSpan DuracionUrl = TimeSpan.FromMinutes(5);

        private readonly IComprobanteService _service;

        public ComprobantesController(IComprobanteService service)
        {
            _service = service;
        }

        /// <summary>Historial completo de una cuenta, incluidos los reemplazados.</summary>
        [HttpGet("cuenta/{idCuenta:long}")]
        public async Task<ActionResult<IEnumerable<ComprobantePago>>> GetPorCuenta(long idCuenta)
        {
            if (!HasAnyRole(RolesComprobante))
                return Forbid();

            return Ok(await _service.ObtenerPorCuentaAsync(idCuenta));
        }

        /// <summary>
        /// URL firmada para que el navegador descargue la imagen directo del bucket, sin
        /// que los bytes pasen por la API. El bucket es privado: sin firma no hay acceso.
        /// </summary>
        [HttpGet("{id:long}/url")]
        public async Task<ActionResult<object>> GetUrl(long id)
        {
            if (!HasAnyRole(RolesComprobante))
                return Forbid();

            var url = await _service.GenerarUrlAsync(id, DuracionUrl);
            if (url is null)
                return NotFound();

            return Ok(new { url, expiraEn = DateTimeOffset.UtcNow.Add(DuracionUrl) });
        }

        [HttpPost]
        [RequestSizeLimit(15 * 1024 * 1024)]
        public async Task<ActionResult<ComprobantePago>> Registrar(
            [FromForm] RegistrarComprobanteForm form,
            CancellationToken ct)
        {
            if (!HasAnyRole(RolesComprobante))
                return Forbid();

            if (form.Archivo is null || form.Archivo.Length == 0)
                return BadRequest(new { message = "Adjunte la imagen del comprobante." });

            // Quien registra sale del token, nunca del cuerpo: si viniera del cliente,
            // cualquiera podria atribuirle un comprobante a otro usuario.
            var idSubidoPor = UsuarioActualId;
            if (idSubidoPor is null)
                return Forbid();

            await using var contenido = form.Archivo.OpenReadStream();

            var solicitud = new RegistrarComprobante
            {
                IdCuenta = form.IdCuenta,
                Contenido = contenido,
                IdSubidoPor = idSubidoPor.Value,
                IdReemplaza = form.IdReemplaza,
                MotivoReemplazo = form.MotivoReemplazo,
            };

            try
            {
                var creado = await _service.RegistrarAsync(solicitud, ct);
                return CreatedAtAction(nameof(GetUrl), new { id = creado.Id }, creado);
            }
            catch (ComprobanteInvalidoException ex)
            {
                // Culpa del archivo que mando el cliente, no del servidor.
                return BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                // Reglas de la base: cuenta que no es QR, reemplazo cruzado entre cuentas,
                // archivo repetido en la misma cuenta. Ver ApiControllerBase.
                var respuesta = TryTranslateDbError(ex);
                if (respuesta is not null)
                    return respuesta;
                throw;
            }
        }

        /// <summary>
        /// Entrada multipart. Separada de <see cref="RegistrarComprobante"/> porque esa
        /// vive en la capa de aplicacion y no debe conocer IFormFile.
        /// </summary>
        public class RegistrarComprobanteForm
        {
            public long IdCuenta { get; set; }
            public IFormFile? Archivo { get; set; }
            public long? IdReemplaza { get; set; }
            public string? MotivoReemplazo { get; set; }
        }
    }
}
