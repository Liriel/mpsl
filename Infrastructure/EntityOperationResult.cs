using System.Collections.Generic;
using mps.Model;
using System.Collections.Specialized;

namespace mps.Infrastructure
{
    public class EntityOperationResult<TEntity> : OperationResult<TEntity> where TEntity : class, IEntity
    {
        public EntityOperationResult(params ValidationError[] validationErrors)
        : base(validationErrors)
        { }
    }
}