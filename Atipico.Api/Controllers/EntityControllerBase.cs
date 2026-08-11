using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

            var created = await _service.AddAsync(entity);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
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

            return NoContent();
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!HasAnyRole(DeleteRoles))
                return Forbid();

            var entity = await _service.GetByIdAsync(id);
            if (entity is null)
                return NotFound();

            await _service.RemoveAsync(entity);
            return NoContent();
        }

        protected bool HasAnyRole(string[] roles) => roles.Any(User.IsInRole);
    }
}
