using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using mps.Model;
using mps.Services;
using WebPush;
using Xunit;
using WebPushSubscription = WebPush.PushSubscription;
using PushSubscriptionModel = mps.Model.PushSubscription;

namespace Tests;

/// <summary>
/// F5: <see cref="WebPushNotificationService"/> batches "someone is active on this list" pushes with a
/// leading-edge throttle (collect window, then a cooldown) and must survive the scope that triggered a
/// call being disposed long before the deferred send runs. Coverage:
/// - throttle shape (collect window collapses bursts, cooldown silences the follow-up, a later call
///   after the cooldown starts a fresh round) - exercised by constructing the service directly against
///   an in-memory configuration and a fake audience/sender, so each test picks its own (tiny) window
///   and cooldown instead of sharing one config across the whole suite;
/// - the (3) regression - the send must not depend on the scope that called NotifyListActivity still
///   being alive - and the audience filter (approved-only, actor excluded) - exercised through
///   <see cref="TestAppFactory"/> against the real database, since both depend on real DI scoping /
///   real role data;
/// - the trigger rule in <see cref="ShoppingListItemEntityService"/> - exercised directly against the
///   service with a fake <see cref="IWebPushNotificationService"/>, which is simpler and faster than
///   driving it through HTTP.
/// </summary>
public class WebPushNotificationTests
{
    // Deliberately not real secrets - VapidDetails only needs *some* strings at construction time,
    // nothing in these tests ever reaches the point of actually signing a push (the sender is faked).
    private const string TestVapidPublicKey = "BEa_VDNyon1zoaJ7333Cc-EcdEeRyva4rSgSR7OXycZNPOMR7vjBIa84FWEBSNSrMkRM_VbyPwmeUbHwp-i6QOY";
    private const string TestVapidPrivateKey = "X3xWg-XvaGEJg0BLtpVcEcCELfCTr_TOyLf8cQVv8T8";

    // ---- helpers shared by the direct-construction (throttle) tests ----

    /// <summary>
    /// Records every send handed to it. Thread-safe: <see cref="WebPushNotificationService"/> fires
    /// sends from a background <c>Task.Run</c>, so a test's assertions race the service's own timer.
    /// Only used by the direct-construction throttle tests below - distinct from the DI-registered
    /// <see cref="RecordingWebPushSender"/> in <c>TestAppFactory.cs</c> that the
    /// <see cref="TestAppFactory"/>-based tests use, to keep those two independent.
    /// </summary>
    private class FakeWebPushSender : IWebPushSender
    {
        private readonly ConcurrentQueue<(string Endpoint, string PayloadJson)> calls = new();

        public IReadOnlyCollection<(string Endpoint, string PayloadJson)> Calls => this.calls.ToArray();

        public Task SendAsync(WebPushSubscription subscription, string payloadJson, VapidDetails vapidDetails)
        {
            this.calls.Enqueue((subscription.Endpoint, payloadJson));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Stubs the audience lookup with a fixed set of (userId, endpoint) subscriptions, applying the
    /// same "exclude the actor" filter <see cref="PushSubscriptionService"/> applies for real so the
    /// throttle tests can assert on the resulting call count without touching a database.
    /// </summary>
    private class FakeSubscriptionService : IPushSubscriptionService
    {
        private readonly List<(string UserId, string Endpoint)> subscribers;

        public FakeSubscriptionService(params (string UserId, string Endpoint)[] subscribers)
        {
            this.subscribers = subscribers.ToList();
        }

        public Task<IEnumerable<PushSubscriptionModel>> GetSubscriptionsForApprovedUsersExceptAsync(string excludeUserId) =>
            Task.FromResult(this.subscribers
                .Where(s => s.UserId != excludeUserId)
                .Select(s => new PushSubscriptionModel { UserId = s.UserId, Endpoint = s.Endpoint, P256dh = "p256dh", Auth = "auth" })
                .AsEnumerable());

        public Task<PushSubscriptionModel> SubscribeAsync(string userId, string endpoint, string p256dh, string auth, string userAgent) =>
            throw new NotImplementedException();

        public Task<bool> UnsubscribeAsync(string userId, string endpoint) => throw new NotImplementedException();

        public Task<IEnumerable<PushSubscriptionModel>> GetUserSubscriptionsAsync(string userId) => throw new NotImplementedException();

        public Task<IEnumerable<PushSubscriptionModel>> GetAllSubscriptionsAsync() => throw new NotImplementedException();

        public Task<PushSubscriptionModel> GetSubscriptionByIdAsync(int subscriptionId) => throw new NotImplementedException();
    }

    /// <summary>
    /// Builds a <see cref="WebPushNotificationService"/> wired to <paramref name="sender"/> and
    /// <paramref name="subscriptionService"/>, with the collect window/cooldown set to
    /// millisecond-scale values so tests never wait anywhere near real production durations.
    /// </summary>
    private static WebPushNotificationService CreateService(
        FakeWebPushSender sender,
        IPushSubscriptionService subscriptionService,
        TimeSpan collectWindow,
        TimeSpan cooldown)
    {
        var services = new ServiceCollection();
        services.AddSingleton(subscriptionService);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VapidKeys:PublicKey"] = TestVapidPublicKey,
            ["VapidKeys:PrivateKey"] = TestVapidPrivateKey,
            ["VapidKeys:Subject"] = "mailto:test@test.local",
            ["PushNotifications:CollectWindowSeconds"] = collectWindow.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["PushNotifications:CooldownMinutes"] = cooldown.TotalMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }).Build();

        return new WebPushNotificationService(scopeFactory, NullLogger<WebPushNotificationService>.Instance, config, sender);
    }

    /// <summary>
    /// Polls <paramref name="condition"/> instead of a fixed <c>Thread.Sleep</c> - the throttle's
    /// sends happen on a background timer, so a fixed sleep is either wastefully long or flaky under
    /// load. Returns as soon as the condition is true, or after <paramref name="timeout"/> elapses.
    /// </summary>
    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;
            await Task.Delay(15);
        }
        return condition();
    }

    private static string GetBody(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        return doc.RootElement.GetProperty("notification").GetProperty("body").GetString()!;
    }

    // ---- 1: collect window collapses a burst into one send per subscriber ----

    [Fact]
    public async Task NotifyListActivity_CallsWithinCollectWindow_ProduceOneSendPerSubscriberReportingTotalCount()
    {
        var sender = new FakeWebPushSender();
        var subs = new FakeSubscriptionService(("actor", "https://push.example.com/actor"),
            ("other-1", "https://push.example.com/other-1"),
            ("other-2", "https://push.example.com/other-2"));
        var service = CreateService(sender, subs, TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(400));

        service.NotifyListActivity(1, "Groceries", "actor", "Actor");
        service.NotifyListActivity(1, "Groceries", "actor", "Actor");

        // two subscribers in the audience (actor's own subscription is excluded) -> two sends, one per
        // subscriber, once the collect window elapses.
        Assert.True(await WaitUntilAsync(() => sender.Calls.Count >= 2, TimeSpan.FromSeconds(5)),
            $"expected 2 sends, got {sender.Calls.Count}");

        // give any wrongly-scheduled extra send a chance to show up before asserting the exact count
        await Task.Delay(100);
        Assert.Equal(2, sender.Calls.Count);
        Assert.All(sender.Calls, call => Assert.Contains("2 items", GetBody(call.PayloadJson)));
        Assert.DoesNotContain(sender.Calls, call => call.Endpoint == "https://push.example.com/actor");
    }

    // ---- 2: a call during the cooldown produces no additional send ----

    [Fact]
    public async Task NotifyListActivity_CallDuringCooldown_ProducesNoAdditionalSend()
    {
        var sender = new FakeWebPushSender();
        var subs = new FakeSubscriptionService(("actor", "https://push.example.com/actor"),
            ("other-1", "https://push.example.com/other-1"));
        var collectWindow = TimeSpan.FromMilliseconds(150);
        var cooldown = TimeSpan.FromMilliseconds(400);
        var service = CreateService(sender, subs, collectWindow, cooldown);

        service.NotifyListActivity(1, "Groceries", "actor", "Actor");
        Assert.True(await WaitUntilAsync(() => sender.Calls.Count >= 1, TimeSpan.FromSeconds(5)));
        var countAfterFirstRound = sender.Calls.Count;

        // fires while still well inside the cooldown that just started
        service.NotifyListActivity(1, "Groceries", "actor", "Actor");

        // wait comfortably longer than window + cooldown so a wrongly-scheduled send would have time
        // to land, then assert nothing new arrived
        await Task.Delay(collectWindow + cooldown + TimeSpan.FromMilliseconds(300));
        Assert.Equal(countAfterFirstRound, sender.Calls.Count);
    }

    // ---- 3: a call after the cooldown elapses starts a second round ----

    [Fact]
    public async Task NotifyListActivity_CallAfterCooldown_ProducesSecondSend()
    {
        var sender = new FakeWebPushSender();
        var subs = new FakeSubscriptionService(("actor", "https://push.example.com/actor"),
            ("other-1", "https://push.example.com/other-1"));
        var collectWindow = TimeSpan.FromMilliseconds(150);
        var cooldown = TimeSpan.FromMilliseconds(250);
        var service = CreateService(sender, subs, collectWindow, cooldown);

        service.NotifyListActivity(1, "Groceries", "actor", "Actor");
        Assert.True(await WaitUntilAsync(() => sender.Calls.Count >= 1, TimeSpan.FromSeconds(5)));
        var countAfterFirstRound = sender.Calls.Count;

        // wait out the cooldown before the next call
        await Task.Delay(collectWindow + cooldown + TimeSpan.FromMilliseconds(150));

        service.NotifyListActivity(1, "Groceries", "actor", "Actor");
        Assert.True(await WaitUntilAsync(() => sender.Calls.Count >= countAfterFirstRound + 1, TimeSpan.FromSeconds(5)),
            $"expected a second round of sends, still at {sender.Calls.Count}");
        Assert.Contains(sender.Calls.Skip(countAfterFirstRound), call => GetBody(call.PayloadJson).Contains("added an item to"));
    }

    // ---- 4: the send must survive the triggering scope being disposed (regression for F5 bug 3) ----

    [Fact]
    public async Task NotifyListActivity_SendsSuccessfully_AfterTriggeringScopeIsDisposed()
    {
        using var factory = new TestAppFactory { PushCollectWindowSeconds = 0.15, PushCooldownMinutes = 0.005 };

        string actorId;
        string otherId;
        int listId;

        using (var setupScope = factory.Services.CreateScope())
        {
            var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            actorId = await CreateApprovedUserAsync(userManager, "scope-actor");
            otherId = await CreateApprovedUserAsync(userManager, "scope-other");

            var db = setupScope.ServiceProvider.GetRequiredService<DataContext>();
            var list = new ShoppingList { Name = "Scope Disposal Test List" };
            db.ShoppingLists.Add(list);
            db.PushSubscriptions.Add(new PushSubscriptionModel
            {
                UserId = otherId,
                Endpoint = "https://push.example.com/scope-other",
                P256dh = "p256dh",
                Auth = "auth",
            });
            await db.SaveChangesAsync();
            listId = list.Id;
        }

        // Resolve the singleton from the root provider (mirrors how it is really injected into other
        // singletons/controllers), call it from inside a scope, then dispose that scope immediately -
        // before the collect window has even elapsed. The old implementation resolved
        // IPushSubscriptionService (and its scoped DataContext) lazily against whatever scope was
        // ambient at call time and blew up with ObjectDisposedException once that scope was gone; the
        // fixed implementation only ever uses scopes it creates itself at send time.
        var webPushService = factory.Services.GetRequiredService<IWebPushNotificationService>();
        using (var triggeringScope = factory.Services.CreateScope())
        {
            webPushService.NotifyListActivity(listId, "Scope Disposal Test List", actorId, "Actor");
        }

        var sender = (RecordingWebPushSender)factory.Services.GetRequiredService<IWebPushSender>();
        Assert.True(await WaitUntilAsync(() => sender.Calls.Any(c => c.Endpoint == "https://push.example.com/scope-other"), TimeSpan.FromSeconds(5)),
            "expected the send to the other approved user's subscription to succeed even though the triggering scope was disposed");
    }

    // ---- 5: the audience is approved users only, excluding the actor ----

    [Fact]
    public async Task NotifyListActivity_Audience_ExcludesActorAndUnapprovedUsers_IncludesOtherApprovedUsers()
    {
        using var factory = new TestAppFactory { PushCollectWindowSeconds = 0.1, PushCooldownMinutes = 0.005 };

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var actorId = await CreateApprovedUserAsync(userManager, "audience-actor");
        var otherApprovedId = await CreateApprovedUserAsync(userManager, "audience-other-approved");

        var unapprovedUser = new ApplicationUser { UserName = "audience-unapproved@test.local", Email = "audience-unapproved@test.local" };
        Assert.True((await userManager.CreateAsync(unapprovedUser)).Succeeded);
        // deliberately no role assigned - mirrors a freshly signed-in, not-yet-approved account

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var list = new ShoppingList { Name = "Audience Test List" };
        db.ShoppingLists.Add(list);
        db.PushSubscriptions.AddRange(
            new PushSubscriptionModel { UserId = actorId, Endpoint = "https://push.example.com/audience-actor", P256dh = "p256dh", Auth = "auth" },
            new PushSubscriptionModel { UserId = otherApprovedId, Endpoint = "https://push.example.com/audience-other-approved", P256dh = "p256dh", Auth = "auth" },
            new PushSubscriptionModel { UserId = unapprovedUser.Id, Endpoint = "https://push.example.com/audience-unapproved", P256dh = "p256dh", Auth = "auth" });
        await db.SaveChangesAsync();
        var listId = list.Id;

        var webPushService = factory.Services.GetRequiredService<IWebPushNotificationService>();
        webPushService.NotifyListActivity(listId, "Audience Test List", actorId, "Actor");

        var sender = (RecordingWebPushSender)factory.Services.GetRequiredService<IWebPushSender>();
        Assert.True(await WaitUntilAsync(() => sender.Calls.Any(c => c.Endpoint == "https://push.example.com/audience-other-approved"), TimeSpan.FromSeconds(5)),
            "expected the other approved user's subscription to receive the notification");

        // give a wrongly-included subscriber a chance to show up before asserting the negative
        await Task.Delay(200);
        Assert.DoesNotContain(sender.Calls, c => c.Endpoint == "https://push.example.com/audience-actor");
        Assert.DoesNotContain(sender.Calls, c => c.Endpoint == "https://push.example.com/audience-unapproved");
    }

    private static async Task<string> CreateApprovedUserAsync(UserManager<ApplicationUser> userManager, string userNamePrefix)
    {
        var user = new ApplicationUser { UserName = $"{userNamePrefix}-{Guid.NewGuid():N}@test.local", Email = $"{userNamePrefix}-{Guid.NewGuid():N}@test.local" };
        var result = await userManager.CreateAsync(user);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        Assert.True((await userManager.AddToRoleAsync(user, "user")).Succeeded);
        return user.Id;
    }

    // ---- 6: the trigger rule - new/revived rows notify, a plain edit does not ----

    /// <summary>
    /// Records every <see cref="IWebPushNotificationService.NotifyListActivity"/> call so the trigger
    /// tests can assert whether <see cref="ShoppingListItemEntityService.AddOrUpdate"/> fired one,
    /// without needing the real throttle/send machinery at all.
    /// </summary>
    private class RecordingFakeWebPushNotificationService : IWebPushNotificationService
    {
        public List<(int ShoppingListId, string ActorUserId)> Calls { get; } = new();

        public void NotifyListActivity(int shoppingListId, string shoppingListName, string actorUserId, string actorName) =>
            this.Calls.Add((shoppingListId, actorUserId));

        public Task SendNotificationAsync(string userId, mps.ViewModels.PushNotificationPayload payload) => Task.CompletedTask;

        public Task<bool> SendTestNotificationAsync(int subscriptionId, string title, string body) => Task.FromResult(true);
    }

    [Fact]
    public async Task AddOrUpdate_NewItem_NotifiesListActivity()
    {
        using var factory = new TestAppFactory();
        using var scope = factory.Services.CreateScope();
        var (svc, fakeWebPush, listId, userId) = await CreateEntityServiceAsync(scope, "trigger-new");

        var item = new ShoppingListItem
        {
            ShoppingListId = listId,
            Name = $"New Item {Guid.NewGuid():N}",
            NormalizedName = "NEW-ITEM",
            ShortName = "Ne",
            AddDate = DateTime.UtcNow,
            AddedByUserId = userId,
        };

        svc.AddOrUpdate(item);

        Assert.Single(fakeWebPush.Calls);
        Assert.Equal((listId, userId), fakeWebPush.Calls[0]);
    }

    [Fact]
    public async Task AddOrUpdate_PlainEditOfOpenItem_DoesNotNotifyListActivity()
    {
        using var factory = new TestAppFactory();
        using var scope = factory.Services.CreateScope();
        var (svc, fakeWebPush, listId, userId) = await CreateEntityServiceAsync(scope, "trigger-edit");

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var item = new ShoppingListItem
        {
            ShoppingListId = listId,
            Name = $"Edit Item {Guid.NewGuid():N}",
            NormalizedName = "EDIT-ITEM",
            ShortName = "Ed",
            Status = ItemState.Open,
            AddDate = DateTime.UtcNow,
            AddedByUserId = userId,
        };
        db.ShoppingListItems.Add(item);
        await db.SaveChangesAsync();

        // mirrors EntityController.Put: fetch the tracked entity and mutate it in place, Open -> Open
        var tracked = db.Find<ShoppingListItem>(item.Id)!;
        tracked.Hint = "changed the hint only";

        svc.AddOrUpdate(tracked);

        Assert.Empty(fakeWebPush.Calls);
    }

    [Fact]
    public async Task AddOrUpdate_ReAddingArchivedItem_NotifiesListActivity()
    {
        using var factory = new TestAppFactory();
        using var scope = factory.Services.CreateScope();
        var (svc, fakeWebPush, listId, userId) = await CreateEntityServiceAsync(scope, "trigger-readd");

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var item = new ShoppingListItem
        {
            ShoppingListId = listId,
            Name = $"Readd Item {Guid.NewGuid():N}",
            NormalizedName = "READD-ITEM",
            ShortName = "Re",
            Status = ItemState.Archived,
            AddDate = DateTime.UtcNow,
            AddedByUserId = userId,
        };
        db.ShoppingListItems.Add(item);
        await db.SaveChangesAsync();

        var tracked = db.Find<ShoppingListItem>(item.Id)!;
        tracked.Status = ItemState.Open;

        svc.AddOrUpdate(tracked);

        Assert.Single(fakeWebPush.Calls);
        Assert.Equal((listId, userId), fakeWebPush.Calls[0]);
    }

    private static async Task<(ShoppingListItemEntityService Service, RecordingFakeWebPushNotificationService FakeWebPush, int ListId, string UserId)> CreateEntityServiceAsync(
        IServiceScope scope, string userNamePrefix)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userId = await CreateApprovedUserAsync(userManager, userNamePrefix);

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var list = new ShoppingList { Name = $"Trigger Test List {Guid.NewGuid():N}" };
        db.ShoppingLists.Add(list);
        await db.SaveChangesAsync();

        var repo = scope.ServiceProvider.GetRequiredService<IRepository>();
        var identityService = scope.ServiceProvider.GetRequiredService<IUserIdentityService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var fakeWebPush = new RecordingFakeWebPushNotificationService();

        var svc = new ShoppingListItemEntityService(repo, identityService, notificationService, fakeWebPush);
        return (svc, fakeWebPush, list.Id, userId);
    }
}
