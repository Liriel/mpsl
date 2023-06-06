using System;
using System.Linq;
using mps.Model;
using mps.Services;
using mps.Infrastructure;

namespace mps.Services
{
    public class EntityService<T> : IEntityService<T> where T : EntityBase
    {
        protected readonly IRepository repo;

        public EntityService(IRepository repo)
        {
            this.repo = repo;
        }

        public virtual IQueryable<T> GetFilteredEntities()
        {
            return this.repo.GetEntities<T>();
        }

        public virtual EntityOperationResult<T> Validate(T entity)
        {
            return new EntityOperationResult<T> { Entity = entity, Success = true };
        }

        public virtual EntityOperationResult<T> AddOrUpdate(T entity)
        {
            if(entity.Id == 0)
                this.repo.Add(entity);

            this.repo.SaveChanges();
            return new EntityOperationResult<T> { Entity = entity, Success = true };
        }

        public virtual EntityOperationResult<T> Delete(T entity)
        {
            this.repo.Remove(entity);
            this.repo.SaveChanges();
            return new EntityOperationResult<T> { Entity = entity, Success = true };
        }
    }
}