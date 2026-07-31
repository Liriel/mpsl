using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using mps.Infrastructure;
using mps.Model;
using Xunit;

namespace Tests;

/// <summary>
/// F7: every persisted <see cref="DateTime"/> is UTC. Pins the <see cref="UtcDateTimeConverter"/>
/// round trip (Kind on read, Local-to-UTC conversion on write), the wire format the Angular client
/// depends on (a trailing "Z"), and the archive cutoff in <c>ShoppingListController.GetItems</c>
/// now comparing UTC to UTC regardless of the container's timezone.
/// </summary>
public class UtcDateTimeTests : IClassFixture<TestAppFactory>, IAsyncLifetime
{
    private readonly TestAppFactory factory;
    private string userId = null!;
    private int shoppingListId;

    public UtcDateTimeTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = this.factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userName = $"utc-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser { UserName = userName, Email = userName };
        var created = await userManager.CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));
        this.userId = user.Id;

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var list = new ShoppingList { Name = $"UTC List {Guid.NewGuid():N}" };
        db.ShoppingLists.Add(list);
        await db.SaveChangesAsync();
        this.shoppingListId = list.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient CreateClient()
    {
        var client = this.factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, this.userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "user");
        return client;
    }

    /// <summary>
    /// AddDate, CheckDate and LastChangedDate are all stamped by production code
    /// (ShoppingListController.AddItem, ItemController.ToggleStatus, PersistedRepository) going
    /// through the real add/toggle endpoints, then reloaded in a fresh scope so the read side of
    /// UtcDateTimeConverter - not just whatever Kind the in-memory entity already had - is what
    /// gets asserted. Every one of them must come back Kind = Utc, or System.Text.Json omits the
    /// trailing "Z" the Angular client relies on.
    /// </summary>
    [Fact]
    public async Task PersistedItem_RoundTripsEveryDateAsUtcKind()
    {
        var name = $"RoundTrip{Random.Shared.Next(10000, 99999)}";

        using var client = this.CreateClient();
        using (var added = await client.PostAsJsonAsync(
            $"api/ShoppingList/{this.shoppingListId}/add",
            new { Name = name, Amount = 1 }))
        {
            added.EnsureSuccessStatusCode();
        }

        int itemId;
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            itemId = db.ShoppingListItems.Single(i => i.Name == name).Id;
        }

        using (var toggled = await client.PutAsync($"api/item/{itemId}/toggle", null))
        {
            toggled.EnsureSuccessStatusCode();
        }

        using var reloadScope = this.factory.Services.CreateScope();
        var reloadDb = reloadScope.ServiceProvider.GetRequiredService<DataContext>();
        var item = reloadDb.ShoppingListItems.Single(i => i.Id == itemId);

        Assert.Equal(DateTimeKind.Utc, item.AddDate.Kind);
        Assert.NotNull(item.CheckDate);
        Assert.Equal(DateTimeKind.Utc, item.CheckDate!.Value.Kind);
        Assert.NotNull(item.LastChangedDate);
        Assert.Equal(DateTimeKind.Utc, item.LastChangedDate!.Value.Kind);
    }

    /// <summary>
    /// Only Local-kinded values are actually converted on write (see the remarks on
    /// UtcDateTimeConverter) - a hand-seeded DateTime.Now, the shape every pre-F7 test seed and any
    /// stray production code used, must still land on the correct UTC instant rather than being
    /// stored as raw local wall-clock text.
    /// </summary>
    [Fact]
    public async Task LocalKindValue_IsConvertedToTheEquivalentUtcInstantOnWrite()
    {
        var localNow = DateTime.Now;
        Assert.Equal(DateTimeKind.Local, localNow.Kind);
        var expectedUtc = localNow.ToUniversalTime();

        int itemId;
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var item = new ShoppingListItem
            {
                ShoppingListId = this.shoppingListId,
                Name = $"LocalWrite{Random.Shared.Next(10000, 99999)}",
                NormalizedName = "LOCALWRITE",
                ShortName = "LW",
                Status = ItemState.Open,
                AddDate = localNow,
                LastChangeByUserId = Constants.SYSUSER_ID,
            };
            db.ShoppingListItems.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;
        }

        using var reloadScope = this.factory.Services.CreateScope();
        var reloadDb = reloadScope.ServiceProvider.GetRequiredService<DataContext>();
        var reloaded = reloadDb.ShoppingListItems.Single(i => i.Id == itemId);

        Assert.Equal(DateTimeKind.Utc, reloaded.AddDate.Kind);
        Assert.True(
            Math.Abs((expectedUtc - reloaded.AddDate).TotalMilliseconds) < 5,
            $"expected ~{expectedUtc:o}, got {reloaded.AddDate:o}");
    }

    /// <summary>
    /// The Angular client parses dates with the browser's Date constructor, which silently
    /// reinterprets a "Z"-less string as local time. This pins the actual JSON on the wire - not
    /// just the server-side Kind - carries the trailing "Z".
    /// </summary>
    [Fact]
    public async Task GetItems_SerializesCheckDateWithTrailingZ()
    {
        var name = $"WireFormat{Random.Shared.Next(10000, 99999)}";

        using var client = this.CreateClient();
        using (var added = await client.PostAsJsonAsync(
            $"api/ShoppingList/{this.shoppingListId}/add",
            new { Name = name, Amount = 1 }))
        {
            added.EnsureSuccessStatusCode();
        }

        int itemId;
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            itemId = db.ShoppingListItems.Single(i => i.Name == name).Id;
        }

        using (var toggled = await client.PutAsync($"api/item/{itemId}/toggle", null))
        {
            toggled.EnsureSuccessStatusCode();
        }

        using var response = await client.GetAsync($"api/ShoppingList/{this.shoppingListId}/item");
        response.EnsureSuccessStatusCode();

        var items = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var mine = items.EnumerateArray().Single(e => e.GetProperty("name").GetString() == name);

        var checkDate = mine.GetProperty("checkDate").GetString();
        Assert.NotNull(checkDate);
        Assert.EndsWith("Z", checkDate);
    }

    /// <summary>
    /// Pins ShoppingListController.cs's one-hour archive cutoff (`DateTime.UtcNow.AddHours(-1)`)
    /// against a container running in any timezone: an item checked 2 hours ago (UTC) is archived
    /// and gains an ItemHistory row, one checked 5 minutes ago is left alone. Before F7 this cutoff
    /// compared a UTC bound to a locally-stamped CheckDate, so it only worked by accident in a UTC
    /// container.
    /// </summary>
    [Fact]
    public async Task GetItems_ArchivesOnlyItemsCheckedOverAnHourAgoInUtc()
    {
        int oldItemId;
        int recentItemId;
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var oldItem = new ShoppingListItem
            {
                ShoppingListId = this.shoppingListId,
                Name = $"OldChecked{Random.Shared.Next(10000, 99999)}",
                NormalizedName = "OLDCHECKED",
                ShortName = "OC",
                Status = ItemState.Checked,
                CheckDate = DateTime.UtcNow.AddHours(-2),
                AddDate = DateTime.UtcNow.AddDays(-1),
                LastChangeByUserId = Constants.SYSUSER_ID,
            };
            var recentItem = new ShoppingListItem
            {
                ShoppingListId = this.shoppingListId,
                Name = $"RecentChecked{Random.Shared.Next(10000, 99999)}",
                NormalizedName = "RECENTCHECKED",
                ShortName = "RC",
                Status = ItemState.Checked,
                CheckDate = DateTime.UtcNow.AddMinutes(-5),
                AddDate = DateTime.UtcNow.AddDays(-1),
                LastChangeByUserId = Constants.SYSUSER_ID,
            };
            db.ShoppingListItems.AddRange(oldItem, recentItem);
            await db.SaveChangesAsync();
            oldItemId = oldItem.Id;
            recentItemId = recentItem.Id;
        }

        using var client = this.CreateClient();
        using var response = await client.GetAsync($"api/ShoppingList/{this.shoppingListId}/item");
        response.EnsureSuccessStatusCode();

        using var verifyScope = this.factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DataContext>();

        var oldItem2 = verifyDb.ShoppingListItems.Single(i => i.Id == oldItemId);
        Assert.Equal(ItemState.Archived, oldItem2.Status);
        Assert.Single(verifyDb.ItemHistories.Where(h => h.ShoppingListItemId == oldItemId));

        var recentItem2 = verifyDb.ShoppingListItems.Single(i => i.Id == recentItemId);
        Assert.Equal(ItemState.Checked, recentItem2.Status);
        Assert.Empty(verifyDb.ItemHistories.Where(h => h.ShoppingListItemId == recentItemId));
    }
}
