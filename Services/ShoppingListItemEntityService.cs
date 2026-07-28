using Microsoft.EntityFrameworkCore;
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

            // capture the pre-save status before base.AddOrUpdate saves - afterwards entity.Id is
            // always non-zero, so this is the only point we can tell "new row" from "edit"
            ItemState? previousStatus = entity.Id == 0
                ? null
                : this.repo.ShoppingListItems.AsNoTracking()
                    .Where(i => i.Id == entity.Id).Select(i => (ItemState?)i.Status).FirstOrDefault();

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

                // Notify list activity only when something genuinely new landed on the list: a brand
                // new row, or one coming back onto the list (e.g. re-adding a previously checked-off
                // item). A plain edit (Open -> Open) stays silent.
                var isNewOnList = previousStatus == null ||
                    (previousStatus != ItemState.Open && entity.Status == ItemState.Open);

                if (isNewOnList && entity.AddedByUserId != null)
                {
                    var shoppingList = this.repo.Find<ShoppingList>(entity.ShoppingListId);
                    var addedByUser = this.repo.Users.FirstOrDefault(u => u.Id == entity.AddedByUserId);

                    if (shoppingList != null && addedByUser != null)
                    {
                        this.webPushService.NotifyListActivity(
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