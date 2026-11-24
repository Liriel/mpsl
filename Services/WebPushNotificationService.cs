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

    /// <summary>
    /// Schedules a "someone is active on this list" push. Leading-edge throttle: the first call for a
    /// given (list, actor) pair opens a short collect window, one notification goes out summarizing
    /// everything that happened in that window, then the pair goes quiet for a cooldown no matter how
    /// much more the actor does. Synchronous - it only schedules work, it never sends inline.
    /// </summary>
    void NotifyListActivity(int shoppingListId, string shoppingListName, string actorUserId, string actorName);

    Task<bool> SendTestNotificationAsync(int subscriptionId, string title, string body);
}

public class WebPushNotificationService : IWebPushNotificationService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<WebPushNotificationService> logger;
    private readonly IWebPushSender webPushSender;
    private readonly VapidDetails vapidDetails;
    private readonly TimeSpan collectWindow;
    private readonly TimeSpan cooldown;

    // Activity tracking: Key is "shoppingListId-actorUserId", Value is the throttle state for that pair
    private readonly ConcurrentDictionary<string, ActivityEntry> activity = new();

    private class ActivityEntry
    {
        public int ShoppingListId { get; set; }
        public string ShoppingListName { get; set; }
        public string ActorUserId { get; set; }
        public string ActorName { get; set; }
        public int ItemCount { get; set; }
        public bool Scheduled { get; set; }
        public DateTime CooldownUntil { get; set; }

        // Set (under this entry's lock) once PruneStaleEntries has removed it from the dictionary.
        // Lets a thread that obtained this entry via GetOrAdd - before the removal - detect that it
        // is now holding an orphaned instance and retry instead of scheduling/mutating a send that
        // would never be looked at again.
        public bool Removed { get; set; }
    }

    public WebPushNotificationService(
        IServiceScopeFactory scopeFactory,
        ILogger<WebPushNotificationService> logger,
        IConfiguration configuration,
        IWebPushSender webPushSender)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        this.webPushSender = webPushSender;

        // Setup VAPID details from configuration
        var publicKey = configuration["VapidKeys:PublicKey"];
        var privateKey = configuration["VapidKeys:PrivateKey"];
        var subject = configuration["VapidKeys:Subject"];

        this.vapidDetails = new VapidDetails(subject, publicKey, privateKey);

        var collectWindowSeconds = configuration.GetValue<double?>("PushNotifications:CollectWindowSeconds") ?? 10;
        var cooldownMinutes = configuration.GetValue<double?>("PushNotifications:CooldownMinutes") ?? 5;

        this.collectWindow = TimeSpan.FromSeconds(collectWindowSeconds);
        this.cooldown = TimeSpan.FromMinutes(cooldownMinutes);
    }

    public async Task SendNotificationAsync(string userId, PushNotificationPayload payload)
    {
        using var scope = this.scopeFactory.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<IPushSubscriptionService>();

        var subscriptions = await subscriptionService.GetUserSubscriptionsAsync(userId);

        foreach (var subscription in subscriptions)
        {
            await SendToSubscriptionAsync(subscriptionService, subscription, payload);
        }
    }

    public void NotifyListActivity(int shoppingListId, string shoppingListName, string actorUserId, string actorName)
    {
        var key = $"{shoppingListId}-{actorUserId}";
        var now = DateTime.UtcNow;
        var shouldSchedule = false;
        ActivityEntry entry;

        // A freshly created entry looks identical to a stale one (unscheduled, CooldownUntil ==
        // MinValue), so it can be pruned by another key's NotifyListActivity call between GetOrAdd and
        // this thread taking the lock. Retry against a fresh GetOrAdd if that happens instead of
        // mutating/scheduling against an entry that is no longer reachable in the dictionary.
        while (true)
        {
            entry = this.activity.GetOrAdd(key, _ => new ActivityEntry
            {
                ShoppingListId = shoppingListId,
                ShoppingListName = shoppingListName,
                ActorUserId = actorUserId,
                ActorName = actorName,
                CooldownUntil = DateTime.MinValue
            });

            lock (entry)
            {
                if (entry.Removed)
                {
                    // orphaned - someone pruned it out from under us, go round again
                    continue;
                }

                // keep the display name/list name fresh even if the entry was created by an older call
                entry.ShoppingListName = shoppingListName;
                entry.ActorName = actorName;

                if (now < entry.CooldownUntil)
                {
                    // still cooling down from a previous notification - stay quiet
                }
                else if (!entry.Scheduled)
                {
                    entry.ItemCount = 1;
                    entry.Scheduled = true;
                    shouldSchedule = true;
                }
                else
                {
                    // already scheduled - fold this activity in, do NOT restart the timer
                    entry.ItemCount++;
                }

                break;
            }
        }

        if (shouldSchedule)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(this.collectWindow);
                    await SendScheduledNotificationAsync(entry);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, $"Error sending list activity notification for {key}");
                }
            });
        }

        PruneStaleEntries(now);
    }

    private async Task SendScheduledNotificationAsync(ActivityEntry entry)
    {
        int itemCount;

        lock (entry)
        {
            itemCount = entry.ItemCount;
            entry.CooldownUntil = DateTime.UtcNow + this.cooldown;
            entry.Scheduled = false;
        }

        try
        {
            var message = itemCount == 1
                ? $"{entry.ActorName} added an item to {entry.ShoppingListName}"
                : $"{entry.ActorName} added {itemCount} items to {entry.ShoppingListName}";

            var payload = new PushNotificationPayload
            {
                Title = "Shopping List Updated",
                Body = message,
                Icon = "/assets/icons/icon-192x192.png",
                Badge = "/assets/icons/icon-72x72.png",
                Tag = $"shopping-list-{entry.ShoppingListId}",
                Data = new ShoppingListNotificationData
                {
                    ShoppingListId = entry.ShoppingListId,
                    ShoppingListName = entry.ShoppingListName,
                    AddedByUser = entry.ActorName,
                    ItemCount = itemCount,
                    Action = "view",
                    OnActionClick = new Dictionary<string, NotificationClickAction>
                    {
                        ["default"] = new NotificationClickAction
                        {
                            Operation = "focusLastFocusedOrOpen",
                            Url = $"/list/{entry.ShoppingListId}"
                        }
                    }
                }
            };

            using var scope = this.scopeFactory.CreateScope();
            var subscriptionService = scope.ServiceProvider.GetRequiredService<IPushSubscriptionService>();

            await SendToListAudienceAsync(subscriptionService, entry.ShoppingListId, entry.ActorUserId, payload);

            this.logger.LogInformation($"Sent list activity notification for {itemCount} item(s) on list {entry.ShoppingListId}");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, $"Error sending list activity notification for list {entry.ShoppingListId}");
        }
    }

    /// <summary>
    /// Prunes entries that are not currently scheduled and whose cooldown has already elapsed, so the
    /// dictionary stays bounded by the number of (list, actor) pairs with genuinely recent activity.
    /// </summary>
    private void PruneStaleEntries(DateTime now)
    {
        foreach (var kvp in this.activity)
        {
            var entry = kvp.Value;

            lock (entry)
            {
                if (entry.Scheduled || now < entry.CooldownUntil)
                    continue;

                // Removal only ever happens while holding the entry's own lock, so a thread that is
                // mid-way through GetOrAdd + lock in NotifyListActivity for this same key either sees
                // this entry before it disappears (blocks on the lock, then observes Removed) or sees
                // it gone afterwards (GetOrAdd creates a fresh one). Either way it never mutates an
                // entry that is no longer reachable from the dictionary.
                entry.Removed = true;
                this.activity.TryRemove(kvp.Key, out _);
            }
        }
    }

    public async Task<bool> SendTestNotificationAsync(int subscriptionId, string title, string body)
    {
        using var scope = this.scopeFactory.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<IPushSubscriptionService>();

        try
        {
            var subscription = await subscriptionService.GetSubscriptionByIdAsync(subscriptionId);
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

            await SendToSubscriptionAsync(subscriptionService, subscription, payload);
            this.logger.LogInformation($"Sent test notification to subscription {subscriptionId}");
            return true;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, $"Error sending test notification to subscription {subscriptionId}");
            return false;
        }
    }

    private async Task SendToListAudienceAsync(IPushSubscriptionService subscriptionService, int shoppingListId, string excludeUserId, PushNotificationPayload payload)
    {
        var subscriptions = await subscriptionService.GetSubscriptionsForApprovedUsersExceptAsync(excludeUserId);

        foreach (var subscription in subscriptions)
        {
            await SendToSubscriptionAsync(subscriptionService, subscription, payload);
        }
    }

    /// <summary>
    /// True for endpoints on the reserved <c>.invalid</c> TLD, which Chrome writes into retired FCM
    /// subscriptions. They can never resolve, so there is nothing to wait for before removing them.
    /// </summary>
    internal static bool IsDeadEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return false;

        return uri.Host.Equals("invalid", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SendToSubscriptionAsync(IPushSubscriptionService subscriptionService, PushSubscriptionModel subscription, PushNotificationPayload payload)
    {
        if (IsDeadEndpoint(subscription.Endpoint))
        {
            this.logger.LogInformation($"Removing subscription {subscription.Id} with an unreachable .invalid endpoint");
            try
            {
                await subscriptionService.UnsubscribeAsync(subscription.UserId, subscription.Endpoint);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Failed to remove dead subscription {subscription.Id}");
            }
            return;
        }

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

            await this.webPushSender.SendAsync(
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
                await subscriptionService.UnsubscribeAsync(subscription.UserId, subscription.Endpoint);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, $"Unexpected error sending push notification to subscription {subscription.Id}");
        }
    }
}
