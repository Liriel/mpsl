using System.Linq;
using mps.Model;
using mps.Infrastructure;

namespace mps.Services
{
    public interface IEntityService<TEntity> where TEntity : class, IEntity
    {
        EntityOperationResult<TEntity> Validate(TEntity entity);

        EntityOperationResult<TEntity> AddOrUpdate(TEntity entity);

        EntityOperationResult<TEntity> Delete(TEntity entity);

        IQueryable<TEntity> GetFilteredEntities();
    }
}