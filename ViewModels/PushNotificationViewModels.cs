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
