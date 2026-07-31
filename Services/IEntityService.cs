using System.Linq;
using mps.Model;
using mps.Infrastructure;

namespace mps.Services
{
    public interface IEntityService<TEntity> where TEntity : EntityBase
    {
        EntityOperationResult<TEntity> Validate(TEntity entity);

        EntityOperationResult<TEntity> AddOrUpdate(TEntity entity);

        EntityOperationResult<TEntity> Delete(TEntity entity);

        /// <summary>
        /// Deletes several entities in a single save, so a bulk operation does not cost one round
        /// trip per entity. Type specific hooks still run once per entity.
        /// </summary>
        /// <returns>The number of entities deleted.</returns>
        int DeleteRange(IEnumerable<TEntity> entities);

        IQueryable<TEntity> GetFilteredEntities();
    }
}