using mps.Model;
using mps.Infrastructure;

namespace mps.Services
{
    public class ShoppingListItemEntityService : EntityService<ShoppingListItem>
    {
        private readonly IUserIdentityService identityService;
        private readonly INotificationService notificationService;

        public ShoppingListItemEntityService(IRepository repo, IUserIdentityService identityService, INotificationService notificationService) : base(repo)
        {
            this.notificationService = notificationService;
            this.identityService = identityService;
        }

        public override EntityOperationResult<ShoppingListItem> AddOrUpdate(ShoppingListItem entity)
        {
            if (entity.Id == 0)
            {
                entity.Status = ItemState.Open;
                if (string.IsNullOrWhiteSpace(entity.ShortName))
                {

                    // TODO: make sure shortname is unique
                    entity.ShortName = NameHelper.GetShortName(entity.Name);
                }
            }

            // call base implementation befor sending the broadcast to get all the 
            // changes (like the id on create)
            var result = base.AddOrUpdate(entity);

            // broadcast removed if the item has been archived
            if (entity.Status == ItemState.Archived)
                this.notificationService.BroadCastItemRemoved(entity);
            else
                this.notificationService.BroadCastItemChanged(entity);

            return result;
        }

        public override EntityOperationResult<ShoppingListItem> Delete(ShoppingListItem entity)
        {
            this.notificationService.BroadCastItemRemoved(entity);
            return base.Delete(entity);
        }

        public override IQueryable<ShoppingListItem> GetFilteredEntities()
        {
            // TODO: add filter
            return base.GetFilteredEntities();
        }

        public override EntityOperationResult<ShoppingListItem> Validate(ShoppingListItem entity)
        {
            if (this.repo.Find<ShoppingList>(entity.ShoppingListId) == null && entity.ShoppingList == null)
                return new EntityOperationResult<ShoppingListItem>(new ValidationError("Invalid shopping list", nameof(ShoppingListItem.ShoppingListId), nameof(ShoppingListItem.ShoppingList)));

            return base.Validate(entity);
        }
    }
}