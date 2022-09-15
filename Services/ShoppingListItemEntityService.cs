using mps.Model;
using mps.Infrastructure;

namespace mps.Services
{
    public class ShoppingListItemEntityService : EntityService<ShoppingListItem>
    {
        private readonly IUserIdentityService identityService;

        public ShoppingListItemEntityService(IRepository repo, IUserIdentityService identityService) : base(repo)
        {
            this.identityService = identityService;
        }

        public override EntityOperationResult<ShoppingListItem> AddOrUpdate(ShoppingListItem entity)
        {
            if (entity.Id == 0)
            {
                entity.Status = ItemState.Open;
                if(string.IsNullOrWhiteSpace(entity.ShortName)){
                    
                    // TODO: make sure shortname is unique
                    entity.ShortName = NameHelper.GetShortName(entity.Name);
                }
            }

            return base.AddOrUpdate(entity);
        }

        public override IQueryable<ShoppingListItem> GetFilteredEntities()
        {
            // TODO: add filter
            return base.GetFilteredEntities();
        }

        public override EntityOperationResult<ShoppingListItem> Validate(ShoppingListItem entity)
        {
            if(this.repo.Find<ShoppingList>(entity.ShoppingListId) == null && entity.ShoppingList == null)
                return new EntityOperationResult<ShoppingListItem>(new ValidationError("Invalid shopping list", nameof(ShoppingListItem.ShoppingListId), nameof(ShoppingListItem.ShoppingList)));

            return base.Validate(entity);
        }
    }
}