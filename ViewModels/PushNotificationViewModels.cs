using System.ComponentModel.DataAnnotations;

namespace mps.ViewModels;

/// <summary>
/// ViewModel for subscribing to push notifications
/// </summary>
public class PushSubscriptionViewModel
{
    [Required]
    public string Endpoint { get; set; }

    [Required]
    public string P256dh { get; set; }

    [Required]
    public string Auth { get; set; }

    public string UserAgent { get; set; }
}

/// <summary>
/// Payload for push notification messages
/// </summary>
public class PushNotificationPayload
{
    public string Title { get; set; }
    public string Body { get; set; }
    public string Icon { get; set; }
    public string Badge { get; set; }
    public string Tag { get; set; }
    public object Data { get; set; }
}

/// <summary>
/// Data for shopping list item notification
/// </summary>
public class ShoppingListNotificationData
{
    public int ShoppingListId { get; set; }
    public string ShoppingListName { get; set; }
    public string AddedByUser { get; set; }
    public int ItemCount { get; set; }
    public string Action { get; set; } = "view";
}
