using System.Linq;
using System.Security.Claims;
using mps.Model;

namespace mps.Services
{
    /// <summary>
    /// Single seam for shopping-list access decisions. Today's product model is
    /// "admin-approved household sharing" - every approved (<c>user</c> or <c>admin</c>) account can
    /// see and modify every shopping list. If per-list ownership/membership is introduced later,
    /// this is the one class to swap; call sites (controllers, hubs) should never need to change.
    /// </summary>
    /// <remarks>
    /// <see cref="ClaimsPrincipal"/> is taken as an explicit parameter rather than resolved from
    /// <see cref="IUserIdentityService"/>/<c>IHttpContextAccessor</c> because the latter is not
    /// reliable inside SignalR hub invocations. Controllers pass <c>this.User</c>, the hub passes
    /// <c>Context.User</c>.
    /// </remarks>
    public interface IShoppingListAccessService
    {
        IQueryable<ShoppingList> FilterAccessible(IQueryable<ShoppingList> lists, ClaimsPrincipal user);

        bool CanAccessList(int shoppingListId, ClaimsPrincipal user);

        bool CanAccessItem(int shoppingListItemId, ClaimsPrincipal user);
    }

    /// <inheritdoc cref="IShoppingListAccessService"/>
    public class ShoppingListAccessService : IShoppingListAccessService
    {
        private readonly IRepository repo;

        public ShoppingListAccessService(IRepository repo)
        {
            this.repo = repo;
        }

        public IQueryable<ShoppingList> FilterAccessible(IQueryable<ShoppingList> lists, ClaimsPrincipal user)
        {
            return IsApproved(user) ? lists : Enumerable.Empty<ShoppingList>().AsQueryable();
        }

        public bool CanAccessList(int shoppingListId, ClaimsPrincipal user)
        {
            return IsApproved(user) && this.repo.Find<ShoppingList>(shoppingListId) != null;
        }

        public bool CanAccessItem(int shoppingListItemId, ClaimsPrincipal user)
        {
            return IsApproved(user) && this.repo.Find<ShoppingListItem>(shoppingListItemId) != null;
        }

        private static bool IsApproved(ClaimsPrincipal user)
        {
            return user != null && (user.IsInRole("user") || user.IsInRole("admin"));
        }
    }
}
