using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using mps.Infrastructure;
using mps.Model;
using Xunit;

namespace Tests;

/// <summary>
/// F6: the assorted correctness bugs. Covers the duplicate-normalized-name 500, the unit handling
/// (silently cleared units, units returned with Id 0), the declutter query's translation and its
/// documented "matches ANY criterion" semantics, and the uniform 400-on-validation-failure
/// contract.
/// </summary>
public class CorrectnessTests : IClassFixture<TestAppFactory>, IAsyncLifetime
{
    private readonly TestAppFactory factory;
    private string userId = null!;
    private int shoppingListId;

    public CorrectnessTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = this.factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userName = $"f6-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser { UserName = userName, Email = userName };
        var created = await userManager.CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));
        this.userId = user.Id;

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var list = new ShoppingList { Name = $"F6 List {Guid.NewGuid():N}" };
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
    /// Seeds an item directly, bypassing the API, so a test can set up state the API would not
    /// produce (an old AddDate, a specific NormalizedName, purchase history).
    /// </summary>
    private async Task<int> SeedItemAsync(
        string name,
        string normalizedName,
        DateTime addDate,
        int? shoppingListId = null,
        params DateTime[] purchaseDates)
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var item = new ShoppingListItem
        {
            ShoppingListId = shoppingListId ?? this.shoppingListId,
            Name = name,
            NormalizedName = normalizedName,
            ShortName = name.Length >= 2 ? name.Substring(0, 2) : name,
            Status = ItemState.Archived,
            AddDate = addDate,
            LastChangeByUserId = Constants.SYSUSER_ID,
        };
        db.ShoppingListItems.Add(item);
        await db.SaveChangesAsync();

        foreach (var purchaseDate in purchaseDates)
            db.ItemHistories.Add(new ItemHistory { ShoppingListItemId = item.Id, CheckDate = purchaseDate });

        if (purchaseDates.Length > 0)
            await db.SaveChangesAsync();

        return item.Id;
    }

    // ---- duplicate NormalizedName ----

    /// <summary>
    /// NormalizedName has no unique index (only Name+ShoppingListId does), so "Öl" and "Oel" both
    /// normalize to OEL. Looking the existing item up with SingleOrDefault threw and answered 500.
    /// </summary>
    [Fact]
    public async Task AddItem_WithDuplicateNormalizedName_DoesNotFail()
    {
        await this.SeedItemAsync("Öl", "OEL", DateTime.UtcNow);
        await this.SeedItemAsync("Oel", "OEL", DateTime.UtcNow);

        using var client = this.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"api/ShoppingList/{this.shoppingListId}/add",
            new { Name = "Öl", Amount = 1 });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"expected success, got {response.StatusCode}: {body}");
    }

    /// <summary>
    /// With several rows collapsing onto one normalized name, the exact name match wins rather
    /// than an arbitrary row.
    /// </summary>
    [Fact]
    public async Task AddItem_WithDuplicateNormalizedName_PrefersTheExactNameMatch()
    {
        await this.SeedItemAsync("Öl", "OEL", DateTime.UtcNow);
        var oelId = await this.SeedItemAsync("Oel", "OEL", DateTime.UtcNow);

        using var client = this.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"api/ShoppingList/{this.shoppingListId}/add",
            new { Name = "Oel", Amount = 7 });

        response.EnsureSuccessStatusCode();

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var reused = db.ShoppingListItems.Single(i => i.Id == oelId);
        Assert.Equal(7, reused.Amount);
        Assert.Equal(ItemState.Open, reused.Status);
    }

    // ---- unit handling ----

    /// <summary>
    /// GetOrCreateUnit used to return a detached unit with Id 0, so the caller stored UnitId = 0.
    /// </summary>
    [Fact]
    public async Task AddItem_WithNewUnit_PersistsTheUnitAndLinksIt()
    {
        var shortName = $"u{Random.Shared.Next(1000, 9999)}";

        using var client = this.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"api/ShoppingList/{this.shoppingListId}/add",
            new { Name = $"Milk {Guid.NewGuid():N}".Substring(0, 20), UnitShortName = shortName, Amount = 2 });

        response.EnsureSuccessStatusCode();

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var unit = db.Units.Single(u => u.ShortName == shortName);
        Assert.NotEqual(0, unit.Id);

        var item = db.ShoppingListItems.Single(i => i.UnitId == unit.Id);
        Assert.Equal(unit.Id, item.UnitId);
    }

    /// <summary>
    /// PostMapping overwrote the unit unconditionally, so an edit that did not resend
    /// UnitShortName silently cleared it.
    /// </summary>
    [Fact]
    public async Task PutItem_WithoutUnitShortName_KeepsTheExistingUnit()
    {
        var shortName = $"k{Random.Shared.Next(1000, 9999)}";
        var name = $"Flour{Random.Shared.Next(10000, 99999)}";

        using var client = this.CreateClient();
        using (var added = await client.PostAsJsonAsync(
            $"api/ShoppingList/{this.shoppingListId}/add",
            new { Name = name, UnitShortName = shortName, Amount = 1 }))
        {
            added.EnsureSuccessStatusCode();
        }

        int itemId;
        int unitId;
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var item = db.ShoppingListItems.Single(i => i.Name == name);
            itemId = item.Id;
            unitId = item.UnitId!.Value;
        }

        // no UnitShortName in the payload at all
        using (var updated = await client.PutAsJsonAsync(
            $"api/item/{itemId}",
            new { Id = itemId, ShoppingListId = this.shoppingListId, Name = name, Amount = 5 }))
        {
            updated.EnsureSuccessStatusCode();
        }

        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var item = db.ShoppingListItems.Single(i => i.Id == itemId);
            Assert.Equal(unitId, item.UnitId);
            Assert.Equal(5, item.Amount);
        }
    }

    /// <summary>
    /// An explicitly empty UnitShortName still means "clear it" - only a missing one is ignored.
    /// </summary>
    [Fact]
    public async Task PutItem_WithEmptyUnitShortName_ClearsTheUnit()
    {
        var shortName = $"g{Random.Shared.Next(1000, 9999)}";
        var name = $"Sugar{Random.Shared.Next(10000, 99999)}";

        using var client = this.CreateClient();
        using (var added = await client.PostAsJsonAsync(
            $"api/ShoppingList/{this.shoppingListId}/add",
            new { Name = name, UnitShortName = shortName, Amount = 1 }))
        {
            added.EnsureSuccessStatusCode();
        }

        int itemId;
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var item = db.ShoppingListItems.Single(i => i.Name == name);
            Assert.NotNull(item.UnitId);
            itemId = item.Id;
        }

        using (var updated = await client.PutAsJsonAsync(
            $"api/item/{itemId}",
            new { Id = itemId, ShoppingListId = this.shoppingListId, Name = name, UnitShortName = "", Amount = 1 }))
        {
            updated.EnsureSuccessStatusCode();
        }

        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.Null(db.ShoppingListItems.Single(i => i.Id == itemId).UnitId);
        }
    }

    // ---- declutter ----

    /// <summary>
    /// The candidate query is now a single grouped LINQ query rather than an AsEnumerable walk over
    /// lazy-loaded history, so it has to translate to SQL. It also implements the documented "ANY
    /// criterion" semantics: a frequently but long ago purchased item is a candidate, which the
    /// previous AND-combined version excluded.
    /// </summary>
    [Fact]
    public async Task Declutter_MatchesAnyCriterionAndRespectsTheAgeGate()
    {
        using var scope0 = this.factory.Services.CreateScope();
        var db0 = scope0.ServiceProvider.GetRequiredService<DataContext>();
        var list = new ShoppingList { Name = $"Declutter {Guid.NewGuid():N}" };
        db0.ShoppingLists.Add(list);
        await db0.SaveChangesAsync();
        var listId = list.Id;

        var now = DateTime.UtcNow;
        var old = now.AddDays(-400);

        // frequently purchased, but not for a year -> stale, so a candidate under OR semantics
        await this.SeedItemAsync("StaleOften", "STALEOFTEN", old, listId,
            now.AddMonths(-12), now.AddMonths(-13), now.AddMonths(-14), now.AddMonths(-15));

        // rarely purchased, but only yesterday -> a candidate on the rare-purchase criterion alone
        await this.SeedItemAsync("RareRecent", "RARERECENT", old, listId, now.AddDays(-1));

        // never purchased and old enough -> a candidate
        await this.SeedItemAsync("NeverOld", "NEVEROLD", old, listId);

        // never purchased but too young -> excluded by the 30 day age gate
        await this.SeedItemAsync("NeverYoung", "NEVERYOUNG", now.AddDays(-5), listId);

        // frequently and recently purchased -> not clutter at all
        await this.SeedItemAsync("FreshOften", "FRESHOFTEN", old, listId,
            now.AddDays(-1), now.AddDays(-3), now.AddDays(-5), now.AddDays(-7));

        using var client = this.CreateClient();
        using var response = await client.GetAsync(
            $"api/ShoppingList/{listId}/declutter?minMonthsSinceLastPurchase=6&maxPurchaseCount=2");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"expected success, got {response.StatusCode}: {body}");

        var names = JsonDocument.Parse(body).RootElement
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToList();

        Assert.Contains("StaleOften", names);
        Assert.Contains("RareRecent", names);
        Assert.Contains("NeverOld", names);
        Assert.DoesNotContain("NeverYoung", names);
        Assert.DoesNotContain("FreshOften", names);
    }

    /// <summary>
    /// The purchase statistics have to survive the move into the database query.
    /// </summary>
    [Fact]
    public async Task Declutter_ReportsPurchaseStatistics()
    {
        using var scope0 = this.factory.Services.CreateScope();
        var db0 = scope0.ServiceProvider.GetRequiredService<DataContext>();
        var list = new ShoppingList { Name = $"Declutter stats {Guid.NewGuid():N}" };
        db0.ShoppingLists.Add(list);
        await db0.SaveChangesAsync();

        var lastPurchase = DateTime.UtcNow.AddDays(-200);
        await this.SeedItemAsync("Counted", "COUNTED", DateTime.UtcNow.AddDays(-400), list.Id,
            lastPurchase, lastPurchase.AddDays(-30), lastPurchase.AddDays(-60));

        using var client = this.CreateClient();
        using var response = await client.GetAsync($"api/ShoppingList/{list.Id}/declutter");
        response.EnsureSuccessStatusCode();

        var candidate = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement
            .EnumerateArray()
            .Single(e => e.GetProperty("name").GetString() == "Counted");

        Assert.Equal(3, candidate.GetProperty("purchaseCount").GetInt32());
        Assert.Equal(200, candidate.GetProperty("daysSinceLastPurchase").GetInt32());
    }

    /// <summary>
    /// Bulk delete now goes through a single save. It must still delete exactly the items asked
    /// for and report the count.
    /// </summary>
    [Fact]
    public async Task Declutter_BulkDelete_RemovesTheItemsInOneBatch()
    {
        var first = await this.SeedItemAsync("BulkA", "BULKA", DateTime.UtcNow.AddDays(-400));
        var second = await this.SeedItemAsync("BulkB", "BULKB", DateTime.UtcNow.AddDays(-400));
        var kept = await this.SeedItemAsync("BulkC", "BULKC", DateTime.UtcNow.AddDays(-400));

        using var client = this.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"api/ShoppingList/{this.shoppingListId}/declutter",
            new[] { first, second });

        response.EnsureSuccessStatusCode();

        var deletedCount = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("deletedCount").GetInt32();
        Assert.Equal(2, deletedCount);

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Empty(db.ShoppingListItems.Where(i => i.Id == first || i.Id == second));
        Assert.Single(db.ShoppingListItems.Where(i => i.Id == kept));
    }

    // ---- error contract ----

    /// <summary>
    /// Validation failures used to come back as 200 with Success = false, which no client could
    /// distinguish from a successful write.
    /// </summary>
    [Fact]
    public async Task Post_WithInvalidModel_Returns400WithValidationErrors()
    {
        using var client = this.CreateClient();
        // Name is [Required] on ShoppingListEditViewModel
        using var response = await client.PostAsJsonAsync("api/ShoppingList", new { Name = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.NotEmpty(root.GetProperty("validationErrors").EnumerateArray());
    }

    /// <summary>
    /// The "already added" rejection was a bare string; it now uses the same result shape.
    /// </summary>
    [Fact]
    public async Task Post_WithNonZeroId_Returns400WithValidationErrors()
    {
        using var client = this.CreateClient();
        using var response = await client.PostAsJsonAsync("api/ShoppingList", new { Id = 42, Name = "Already there" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.NotEmpty(root.GetProperty("validationErrors").EnumerateArray());
    }

    /// <summary>
    /// A valid write is unaffected and still answers 200 with the saved entity.
    /// </summary>
    [Fact]
    public async Task Post_WithValidModel_StillReturns200()
    {
        using var client = this.CreateClient();
        using var response = await client.PostAsJsonAsync("api/ShoppingList", new { Name = $"Valid {Guid.NewGuid():N}" });

        response.EnsureSuccessStatusCode();

        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.NotEqual(0, root.GetProperty("entity").GetProperty("id").GetInt32());
    }
}
