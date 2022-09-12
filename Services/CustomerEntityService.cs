using System.Linq;
using mps.Model;
using mps.Services;
using mps.Infrastructure;

namespace mps.Services
{
    public class ShoppingListEntityService : EntityService<ShoppingList>
    {

        public ShoppingListEntityService(IRepository repo) : base(repo)
        {
        }

        public override EntityOperationResult<ShoppingList> AddOrUpdate(ShoppingList entity)
        {
            // TODO: custom code
            return base.AddOrUpdate(entity);
        }

        public override IQueryable<ShoppingList> GetFilteredEntities()
        {
            // TODO: add filter
            return base.GetFilteredEntities();
        }
    }
}