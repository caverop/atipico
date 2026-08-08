using Atipico.Application.Interfaces.Services;
using Atipico.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Atipico.Api.Controllers
{
    [ApiController]
    public abstract class EntityControllerBase<TEntity> : ControllerBase where TEntity : class, IEntity
    {
        private readonly IEntityService<TEntity> _service;

        protected EntityControllerBase(IEntityService<TEntity> service)
        {
            _service = service;
        }

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
        public async Task<ActionResult<TEntity>> Create(TEntity entity)
        {
            var created = await _service.AddAsync(entity);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(long id, TEntity entity)
        {
            if (id != entity.Id)
                return BadRequest("El id de la ruta no coincide con el id del cuerpo.");

            if (await _service.GetByIdAsync(id) is null)
                return NotFound();

            await _service.UpdateAsync(entity);
            return NoContent();
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var entity = await _service.GetByIdAsync(id);
            if (entity is null)
                return NotFound();

            await _service.RemoveAsync(entity);
            return NoContent();
        }
    }
}
