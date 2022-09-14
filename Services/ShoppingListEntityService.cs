using System.Linq;
using mps.Model;
using mps.Services;
using mps.Infrastructure;

namespace mps.Services
{
    public class ShoppingListEntityService : EntityService<ShoppingList>
    {
        private readonly IUserIdentityService identityService;

        public ShoppingListEntityService(IRepository repo, IUserIdentityService identityService) : base(repo)
        {
            this.identityService = identityService;
        }

        public override EntityOperationResult<ShoppingList> AddOrUpdate(ShoppingList entity)
        {
            if (entity.Id == 0)
            {
                entity.OwnerUserKey = this.identityService.CurrentUserId ?? Constants.SYSUSER_ID;
            }

            return base.AddOrUpdate(entity);
        }

        public override IQueryable<ShoppingList> GetFilteredEntities()
        {
            // TODO: add filter
            return base.GetFilteredEntities();
        }
    }
}