using mps.Model;

namespace mps.Services;

public interface INotificationService
{
    Task BroadCastItemChanged(ShoppingListItem item);
    Task BroadCastItemRemoved(ShoppingListItem item);
}
