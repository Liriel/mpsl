using mps.Model;
using mps.Infrastructure;

namespace mps.Services
{
    public class ShoppingListItemEntityService : EntityService<ShoppingListItem>
    {
        private readonly IUserIdentityService identityService;
        private readonly INotificationService notificationService;
        private readonly IWebPushNotificationService webPushService;

        public ShoppingListItemEntityService(
            IRepository repo, 
            IUserIdentityService identityService, 
            INotificationService notificationService,
            IWebPushNotificationService webPushService) : base(repo)
        {
            this.notificationService = notificationService;
            this.identityService = identityService;
            this.webPushService = webPushService;
        }

        public override EntityOperationResult<ShoppingListItem> AddOrUpdate(ShoppingListItem entity)
        {
            if (entity.Id == 0)
            {
                entity.Status = ItemState.Open;
                if (string.IsNullOrWhiteSpace(entity.ShortName))
                {
                    int offset = 1;
                    do{
                        entity.ShortName = NameHelper.GetShortName(entity.Name, 2, offset);
                        offset++;

                    // make sure shortname is unique
                    }while(this.repo.ShoppingListItems.Any(i=>i.ShoppingListId == entity.ShoppingListId && i.ShortName == entity.ShortName));
                }
            }

            // call base implementation befor sending the broadcast to get all the 
            // changes (like the id on create)
            var result = base.AddOrUpdate(entity);

            // broadcast removed if the item has been archived
            if (entity.Status == ItemState.Archived)
            {
                this.notificationService.BroadCastItemRemoved(entity);
            }
            else
            {
                this.notificationService.BroadCastItemChanged(entity);

                // Send batched push notification for new items (not for updates)
                if (entity.Id != 0 && entity.AddedByUserId != null)
                {
                    var shoppingList = this.repo.Find<ShoppingList>(entity.ShoppingListId);
                    var addedByUser = this.repo.Users.FirstOrDefault(u => u.Id == entity.AddedByUserId);
                    
                    if (shoppingList != null && addedByUser != null)
                    {
                        // Fire and forget - don't await
                        _ = this.webPushService.SendBatchedShoppingListNotificationAsync(
                            shoppingList.Id,
                            shoppingList.Name,
                            addedByUser.Id,
                            addedByUser.ShortName ?? addedByUser.UserName
                        );
                    }
                }
            }

            return result;
        }

        public override EntityOperationResult<ShoppingListItem> Delete(ShoppingListItem entity)
        {
            this.notificationService.BroadCastItemRemoved(entity);
            return base.Delete(entity);
        }

        public override EntityOperationResult<ShoppingListItem> Validate(ShoppingListItem entity)
        {
            if (this.repo.Find<ShoppingList>(entity.ShoppingListId) == null && entity.ShoppingList == null)
                return new EntityOperationResult<ShoppingListItem>(new ValidationError("Invalid shopping list", nameof(ShoppingListItem.ShoppingListId), nameof(ShoppingListItem.ShoppingList)));

            return base.Validate(entity);
        }
    }
}