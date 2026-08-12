using Atipico.Application.Common.Interfaces;
using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atipico.Api.Controllers
{
    [Route("api/usuarios")]
    public class UsuariosController : EntityControllerBase<Usuario>
    {
        private readonly IPasswordHasher _passwordHasher;

        public UsuariosController(IEntityService<Usuario> service, IPasswordHasher passwordHasher) : base(service)
        {
            _passwordHasher = passwordHasher;
        }

        public override async Task<ActionResult<Usuario>> Create(Usuario entity)
        {
            if (!HasAnyRole(CreateRoles))
                return Forbid();

            if (string.IsNullOrWhiteSpace(entity.Password))
                return BadRequest("Password es requerido para crear un usuario.");

            entity.PasswordHash = _passwordHasher.Hash(entity.Password);
            entity.Password = null;

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

        public override async Task<IActionResult> Update(long id, Usuario entity)
        {
            if (!HasAnyRole(UpdateRoles))
                return Forbid();

            if (id != entity.Id)
                return BadRequest("El id de la ruta no coincide con el id del cuerpo.");

            // Mutamos la instancia ya rastreada por EF en vez de adjuntar un reemplazo:
            // así evitamos el conflicto de "misma clave, dos instancias" y de paso
            // preservamos el password_hash cuando no viene una contraseña nueva.
            var existing = await _service.GetByIdAsync(id);
            if (existing is null)
                return NotFound();

            existing.Nombre = entity.Nombre;
            existing.NombreUsuario = entity.NombreUsuario;
            existing.Rol = entity.Rol;
            existing.Activo = entity.Activo;

            if (!string.IsNullOrWhiteSpace(entity.Password))
                existing.PasswordHash = _passwordHasher.Hash(entity.Password);

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

            return NoContent();
        }
    }
}
