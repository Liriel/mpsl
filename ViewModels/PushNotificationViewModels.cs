using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
}

/// <summary>
/// Payload for push notification messages
/// Follows Web Notification API standard with camelCase properties
/// </summary>
public class PushNotificationPayload
{
    [JsonPropertyName("title")]
    public string Title { get; set; }
    
    [JsonPropertyName("body")]
    public string Body { get; set; }
    
    [JsonPropertyName("icon")]
    public string Icon { get; set; }
    
    [JsonPropertyName("badge")]
    public string Badge { get; set; }
    
    [JsonPropertyName("tag")]
    public string Tag { get; set; }
    
    [JsonPropertyName("data")]
    public object Data { get; set; }
}

/// <summary>
/// Data for shopping list item notification
/// </summary>
public class ShoppingListNotificationData
{
    [JsonPropertyName("shoppingListId")]
    public int ShoppingListId { get; set; }
    
    [JsonPropertyName("shoppingListName")]
    public string ShoppingListName { get; set; }
    
    [JsonPropertyName("addedByUser")]
    public string AddedByUser { get; set; }
    
    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }
    
    [JsonPropertyName("action")]
    public string Action { get; set; } = "view";

    /// <summary>
    /// Read by the Angular service worker (ngsw) on notification click. Without it the worker only
    /// forwards the click to already-open windows, so a click with the app closed does nothing.
    /// "focusLastFocusedOrOpen" focuses a running app (whose <c>notificationClicks</c> handler then
    /// routes to the list without a reload) or opens a new window directly on the list.
    /// </summary>
    [JsonPropertyName("onActionClick")]
    public Dictionary<string, NotificationClickAction> OnActionClick { get; set; }
}

/// <summary>
/// One entry of ngsw's <c>data.onActionClick</c> map, keyed by action ("default" for the body click).
/// </summary>
public class NotificationClickAction
{
    [JsonPropertyName("operation")]
    public string Operation { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}

/// <summary>
/// Request to send a test notification
/// </summary>
public class TestNotificationRequest
{
    [Required]
    public int SubscriptionId { get; set; }

    [Required]
    public string Title { get; set; }

    [Required]
    public string Body { get; set; }
}
