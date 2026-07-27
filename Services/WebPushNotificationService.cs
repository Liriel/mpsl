using System.Collections.Concurrent;
using System.Text.Json;
using WebPush;
using mps.Model;
using mps.ViewModels;
using PushSubscriptionModel = mps.Model.PushSubscription;
using WebPushSubscription = WebPush.PushSubscription;

namespace mps.Services;

public interface IWebPushNotificationService
{
    Task SendNotificationAsync(string userId, PushNotificationPayload payload);
    Task SendNotificationToAllExceptAsync(string excludeUserId, PushNotificationPayload payload);
    Task SendBatchedShoppingListNotificationAsync(int shoppingListId, string shoppingListName, string addedByUserId, string addedByUserName);
    Task<bool> SendTestNotificationAsync(int subscriptionId, string title, string body);
}

public class WebPushNotificationService : IWebPushNotificationService
{
    private readonly IPushSubscriptionService subscriptionService;
    private readonly ILogger<WebPushNotificationService> logger;
    private readonly IConfiguration configuration;
    private readonly WebPushClient webPushClient;
    private readonly VapidDetails vapidDetails;

    // Batch tracking: Key is "shoppingListId-userId", Value is batch info
    private readonly ConcurrentDictionary<string, BatchInfo> pendingBatches = new();

    private class BatchInfo
    {
        public int ShoppingListId { get; set; }
        public string ShoppingListName { get; set; }
        public string AddedByUserId { get; set; }
        public string AddedByUserName { get; set; }
        public int ItemCount { get; set; }
        public CancellationTokenSource CancellationToken { get; set; }
    }

    public WebPushNotificationService(
        IPushSubscriptionService subscriptionService,
        ILogger<WebPushNotificationService> logger,
        IConfiguration configuration)
    {
        this.subscriptionService = subscriptionService;
        this.logger = logger;
        this.configuration = configuration;

        this.webPushClient = new WebPushClient();

        // Setup VAPID details from configuration
        var publicKey = configuration["VapidKeys:PublicKey"];
        var privateKey = configuration["VapidKeys:PrivateKey"];
        var subject = configuration["VapidKeys:Subject"];

        this.vapidDetails = new VapidDetails(subject, publicKey, privateKey);
    }

    public async Task SendNotificationAsync(string userId, PushNotificationPayload payload)
    {
        var subscriptions = await this.subscriptionService.GetUserSubscriptionsAsync(userId);

        foreach (var subscription in subscriptions)
        {
            await SendToSubscriptionAsync(subscription, payload);
        }
    }

    public async Task SendNotificationToAllExceptAsync(string excludeUserId, PushNotificationPayload payload)
    {
        var subscriptions = await this.subscriptionService.GetAllSubscriptionsExceptUserAsync(excludeUserId);

        foreach (var subscription in subscriptions)
        {
            await SendToSubscriptionAsync(subscription, payload);
        }
    }

    public async Task SendBatchedShoppingListNotificationAsync(
        int shoppingListId,
        string shoppingListName,
        string addedByUserId,
        string addedByUserName)
    {
        var batchKey = $"{shoppingListId}-{addedByUserId}";

        // Cancel existing timer if one exists
        if (pendingBatches.TryGetValue(batchKey, out var existingBatch))
        {
            existingBatch.CancellationToken.Cancel();
            existingBatch.ItemCount++;
        }
        else
        {
            existingBatch = new BatchInfo
            {
                ShoppingListId = shoppingListId,
                ShoppingListName = shoppingListName,
                AddedByUserId = addedByUserId,
                AddedByUserName = addedByUserName,
                ItemCount = 1,
                CancellationToken = new CancellationTokenSource()
            };
            pendingBatches[batchKey] = existingBatch;
        }

        // Wait 30 seconds before sending (debounce)
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(30000, existingBatch.CancellationToken.Token);

                // Time elapsed without new items, send the notification
                if (pendingBatches.TryRemove(batchKey, out var batch))
                {
                    var message = batch.ItemCount == 1
                        ? $"{batch.AddedByUserName} added an item to {batch.ShoppingListName}"
                        : $"{batch.AddedByUserName} added {batch.ItemCount} items to {batch.ShoppingListName}";

                    var payload = new PushNotificationPayload
                    {
                        Title = "Shopping List Updated",
                        Body = message,
                        Icon = "/assets/icons/icon-192x192.png",
                        Badge = "/assets/icons/icon-72x72.png",
                        Tag = $"shopping-list-{batch.ShoppingListId}",
                        Data = new ShoppingListNotificationData
                        {
                            ShoppingListId = batch.ShoppingListId,
                            ShoppingListName = batch.ShoppingListName,
                            AddedByUser = batch.AddedByUserName,
                            ItemCount = batch.ItemCount,
                            Action = "view"
                        }
                    };

                    await SendNotificationToAllExceptAsync(batch.AddedByUserId, payload);
                    this.logger.LogInformation($"Sent batched notification for {batch.ItemCount} items to list {batch.ShoppingListId}");
                }
            }
            catch (TaskCanceledException)
            {
                // Timer was cancelled, new item was added
                this.logger.LogDebug($"Batch notification timer cancelled for {batchKey}");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Error sending batched notification for {batchKey}");
                pendingBatches.TryRemove(batchKey, out _);
            }
        });

        await Task.CompletedTask;
    }

    public async Task<bool> SendTestNotificationAsync(int subscriptionId, string title, string body)
    {
        try
        {
            var subscription = await this.subscriptionService.GetSubscriptionByIdAsync(subscriptionId);
            if (subscription == null)
            {
                this.logger.LogWarning($"Subscription {subscriptionId} not found");
                return false;
            }

            var payload = new PushNotificationPayload
            {
                Title = title,
                Body = body,
                Icon = "/assets/icons/icon-192x192.png",
                Badge = "/assets/icons/icon-72x72.png",
                Tag = $"test-notification-{DateTime.UtcNow.Ticks}",
                Data = new { Type = "test", Timestamp = DateTime.UtcNow }
            };

            await SendToSubscriptionAsync(subscription, payload);
            this.logger.LogInformation($"Sent test notification to subscription {subscriptionId}");
            return true;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, $"Error sending test notification to subscription {subscriptionId}");
            return false;
        }
    }

    private async Task SendToSubscriptionAsync(PushSubscriptionModel subscription, PushNotificationPayload payload)
    {
        try
        {
            var pushSubscription = new WebPushSubscription(
                subscription.Endpoint,
                subscription.P256dh,
                subscription.Auth
            );

            // Wrap payload in notification object as expected by Angular Service Worker
            // and Web Push API standard
            var wrappedPayload = new { notification = payload };
            var payloadJson = JsonSerializer.Serialize(wrappedPayload);

            await this.webPushClient.SendNotificationAsync(
                pushSubscription,
                payloadJson,
                this.vapidDetails
            );

            this.logger.LogDebug($"Sent push notification to subscription {subscription.Id}");
        }
        catch (WebPushException ex)
        {
            this.logger.LogError(ex, $"Error sending push notification to subscription {subscription.Id}");

            // If subscription is no longer valid (410 Gone, 404 Not Found), remove it
            if (ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                this.logger.LogInformation($"Removing invalid subscription {subscription.Id}");
                await this.subscriptionService.UnsubscribeAsync(subscription.UserId, subscription.Endpoint);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, $"Unexpected error sending push notification to subscription {subscription.Id}");
        }
    }
}
