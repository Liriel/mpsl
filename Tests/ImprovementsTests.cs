using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using mps.Infrastructure;
using mps.Model;
using mps.Services;
using Xunit;

namespace Tests;

/// <summary>
/// I1 (archiving is race free and the history index rejects duplicates) and I2 (names are trimmed
/// before duplicate detection, whitespace-only names are rejected).
/// </summary>
public class ImprovementsTests : IClassFixture<TestAppFactory>, IAsyncLifetime
{
    private readonly TestAppFactory factory;
    private string userId = null!;
    private int shoppingListId;

    public ImprovementsTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = this.factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userName = $"i-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser { UserName = userName, Email = userName };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        this.userId = user.Id;

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var list = new ShoppingList { Name = $"I List {Guid.NewGuid():N}" };
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

    private async Task<int> SeedCheckedItemAsync(string name, DateTime checkDate)
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var item = new ShoppingListItem
        {
            ShoppingListId = this.shoppingListId,
            Name = name,
            NormalizedName = NameHelper.GetNormalizedName(name),
            ShortName = "XX",
            Status = ItemState.Checked,
            CheckDate = checkDate,
            AddDate = checkDate.AddMinutes(-5),
            Amount = 3,
            LastChangeByUserId = Constants.SYSUSER_ID,
        };
        db.ShoppingListItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private int HistoryCount(int itemId)
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        return db.ItemHistories.Count(h => h.ShoppingListItemId == itemId);
    }

    // ---- I1 ----

    [Fact]
    public async Task GetItems_ConcurrentCalls_WriteExactlyOneHistoryRow()
    {
        var itemId = await this.SeedCheckedItemAsync("Race", DateTime.UtcNow.AddHours(-3));

        using var client = this.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => client.GetAsync($"api/ShoppingList/{this.shoppingListId}/item"))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, this.HistoryCount(itemId));

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(ItemState.Archived, db.ShoppingListItems.Single(i => i.Id == itemId).Status);
    }

    [Fact]
    public async Task ArchiveStale_RecordsAmountAndSecondCallIsANoOp()
    {
        var checkDate = DateTime.UtcNow.AddHours(-2);
        var itemId = await this.SeedCheckedItemAsync("Once", checkDate);

        using var scope = this.factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IItemArchiveService>();

        Assert.Equal(1, service.ArchiveStale(this.shoppingListId, DateTime.UtcNow.AddHours(-1)));
        Assert.Equal(0, service.ArchiveStale(this.shoppingListId, DateTime.UtcNow.AddHours(-1)));

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var history = Assert.Single(db.ItemHistories.Where(h => h.ShoppingListItemId == itemId).ToList());
        Assert.Equal(3, history.Amount);
        Assert.Equal(DateTimeKind.Utc, history.CheckDate.Kind);
    }

    [Fact]
    public async Task ArchiveStale_LeavesRecentlyCheckedItemsAlone()
    {
        var itemId = await this.SeedCheckedItemAsync("Fresh", DateTime.UtcNow.AddMinutes(-5));

        using var scope = this.factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IItemArchiveService>();
        Assert.Equal(0, service.ArchiveStale(this.shoppingListId, DateTime.UtcNow.AddHours(-1)));
        Assert.Equal(0, this.HistoryCount(itemId));
    }

    [Fact]
    public async Task UniqueHistoryIndex_RejectsADuplicateInsert()
    {
        var checkDate = DateTime.UtcNow.AddDays(-1);
        var itemId = await this.SeedCheckedItemAsync("Index", checkDate);

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        db.ItemHistories.Add(new ItemHistory { ShoppingListItemId = itemId, CheckDate = checkDate });
        await db.SaveChangesAsync();

        db.ItemHistories.Add(new ItemHistory { ShoppingListItemId = itemId, CheckDate = checkDate });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // ---- I2 ----

    [Theory]
    [InlineData("Milch ", "MILCH")]
    [InlineData("  Pass   Tomaten\t", "PASS TOMATEN")]
    [InlineData("Käse ", "KAESE")]
    public void GetNormalizedName_TrimsAndCollapsesWhitespace(string input, string expected) =>
        Assert.Equal(expected, NameHelper.GetNormalizedName(input));

    [Fact]
    public async Task AddItem_WithTrailingSpace_ReusesTheExistingItem()
    {
        using var client = this.CreateClient();
        using var first = await client.PostAsJsonAsync($"api/ShoppingList/{this.shoppingListId}/add", new { Name = "Milch" });
        first.EnsureSuccessStatusCode();
        using var second = await client.PostAsJsonAsync($"api/ShoppingList/{this.shoppingListId}/add", new { Name = "Milch " });
        second.EnsureSuccessStatusCode();

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var item = Assert.Single(db.ShoppingListItems.Where(i => i.ShoppingListId == this.shoppingListId && i.NormalizedName == "MILCH").ToList());
        Assert.Equal("Milch", item.Name);
    }

    [Fact]
    public async Task AddItem_StoresTheTrimmedName()
    {
        using var client = this.CreateClient();
        using var response = await client.PostAsJsonAsync($"api/ShoppingList/{this.shoppingListId}/add", new { Name = "  Quark  " });
        response.EnsureSuccessStatusCode();

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var item = db.ShoppingListItems.Single(i => i.ShoppingListId == this.shoppingListId && i.NormalizedName == "QUARK");
        Assert.Equal("Quark", item.Name);
    }

    [Fact]
    public async Task EditItem_StoresTheTrimmedName()
    {
        using var client = this.CreateClient();
        using var added = await client.PostAsJsonAsync($"api/ShoppingList/{this.shoppingListId}/add", new { Name = "Butter" });
        added.EnsureSuccessStatusCode();
        int itemId;
        using (var scope = this.factory.Services.CreateScope())
            itemId = scope.ServiceProvider.GetRequiredService<DataContext>().ShoppingListItems
                .Single(i => i.ShoppingListId == this.shoppingListId && i.Name == "Butter").Id;

        using var updated = await client.PutAsJsonAsync($"api/item/{itemId}",
            new { Id = itemId, ShoppingListId = this.shoppingListId, Name = " Butter2 " });
        updated.EnsureSuccessStatusCode();

        using var verify = this.factory.Services.CreateScope();
        var item = verify.ServiceProvider.GetRequiredService<DataContext>().ShoppingListItems.Single(i => i.Id == itemId);
        Assert.Equal("Butter2", item.Name);
        Assert.Equal("BUTTER2", item.NormalizedName);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public async Task AddItem_WithWhitespaceOnlyName_Returns400(string name)
    {
        using var client = this.CreateClient();
        using var response = await client.PostAsJsonAsync($"api/ShoppingList/{this.shoppingListId}/add", new { Name = name });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- I6 ----

    [Theory]
    [InlineData(null, "500g", 500, "g")]
    [InlineData(0, "500g", 500, "g")]
    [InlineData(null, "500 g", 500, "g")]
    [InlineData(null, "1KG", 1, "KG")]
    [InlineData(null, "2x", 2, "")]
    [InlineData(null, "2X", 2, "")]
    [InlineData(null, "3", 3, "")]
    [InlineData(null, "6er", 6, "")]
    [InlineData(null, "250ml", 250, "ml")]
    [InlineData(null, "3 Stück", 3, "Stück")]
    [InlineData(5, "500g", 5, "500g")]
    [InlineData(null, "g", null, "g")]
    [InlineData(null, "kg2", null, "kg2")]
    [InlineData(null, "...", null, "...")]
    [InlineData(null, "99999999999", null, "99999999999")]
    [InlineData(null, "", null, "")]
    [InlineData(null, null, null, null)]
    public void UnitParser_Split_Table(int? amount, string? unitText, int? expectedAmount, string? expectedUnit)
    {
        var (a, u) = UnitParser.Split(amount, unitText!);
        Assert.Equal(expectedAmount, a);
        Assert.Equal(expectedUnit, u);
    }

    [Theory]
    [InlineData("g", true)]
    [InlineData("Stk", true)]
    [InlineData("10", false)]
    [InlineData("...", false)]
    [InlineData("&", false)]
    [InlineData(" ", false)]
    public void UnitParser_IsPlausibleUnit_Table(string text, bool expected) =>
        Assert.Equal(expected, UnitParser.IsPlausibleUnit(text));

    [Fact]
    public async Task AddItem_WithQuantityInUnitField_SplitsAmountAndUnit()
    {
        using var client = this.CreateClient();
        using var response = await client.PostAsJsonAsync($"api/ShoppingList/{this.shoppingListId}/add",
            new { Name = "Mehl I6", UnitShortName = "500g" });
        response.EnsureSuccessStatusCode();

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var item = db.ShoppingListItems.Include(i => i.Unit)
            .Single(i => i.ShoppingListId == this.shoppingListId && i.Name == "Mehl I6");
        Assert.Equal(500, item.Amount);
        Assert.Equal("g", item.Unit?.ShortName);
    }

    [Fact]
    public async Task AddItem_WithCountMarkerUnit_StoresAmountOnly()
    {
        using var client = this.CreateClient();
        using var response = await client.PostAsJsonAsync($"api/ShoppingList/{this.shoppingListId}/add",
            new { Name = "Eier I6", UnitShortName = "6er" });
        response.EnsureSuccessStatusCode();

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var item = db.ShoppingListItems.Single(i => i.ShoppingListId == this.shoppingListId && i.Name == "Eier I6");
        Assert.Equal(6, item.Amount);
        Assert.Null(item.UnitId);
    }

    [Theory]
    [InlineData("...")]
    [InlineData("&")]
    public async Task AddItem_WithPunctuationOnlyUnit_Returns400OnUnitField(string unit)
    {
        using var client = this.CreateClient();
        using var response = await client.PostAsJsonAsync($"api/ShoppingList/{this.shoppingListId}/add",
            new { Name = "Junk I6", UnitShortName = unit, Amount = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        var error = Assert.Single(body.GetProperty("validationErrors").EnumerateArray());
        Assert.Equal("UnitShortName", error.GetProperty("fields")[0].GetString());

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.False(db.Units.Any(u => u.ShortName == unit));
    }

    [Fact]
    public async Task PutItem_WithPunctuationOnlyUnit_Returns400OnUnitField()
    {
        using var client = this.CreateClient();
        using var added = await client.PostAsJsonAsync($"api/ShoppingList/{this.shoppingListId}/add", new { Name = "Put I6" });
        added.EnsureSuccessStatusCode();
        int itemId;
        using (var scope = this.factory.Services.CreateScope())
            itemId = scope.ServiceProvider.GetRequiredService<DataContext>().ShoppingListItems
                .Single(i => i.ShoppingListId == this.shoppingListId && i.Name == "Put I6").Id;

        using var updated = await client.PutAsJsonAsync($"api/item/{itemId}",
            new { Id = itemId, ShoppingListId = this.shoppingListId, Name = "Put I6", UnitShortName = "--" });
        Assert.Equal(HttpStatusCode.BadRequest, updated.StatusCode);
        var body = await updated.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("UnitShortName", body.GetProperty("validationErrors")[0].GetProperty("fields")[0].GetString());
    }

    [Fact]
    public async Task PutItem_WithQuantityInUnitField_SplitsAmountAndUnit()
    {
        using var client = this.CreateClient();
        using var added = await client.PostAsJsonAsync($"api/ShoppingList/{this.shoppingListId}/add", new { Name = "Put2 I6" });
        added.EnsureSuccessStatusCode();
        int itemId;
        using (var scope = this.factory.Services.CreateScope())
            itemId = scope.ServiceProvider.GetRequiredService<DataContext>().ShoppingListItems
                .Single(i => i.ShoppingListId == this.shoppingListId && i.Name == "Put2 I6").Id;

        using var updated = await client.PutAsJsonAsync($"api/item/{itemId}",
            new { Id = itemId, ShoppingListId = this.shoppingListId, Name = "Put2 I6", UnitShortName = "250ml" });
        updated.EnsureSuccessStatusCode();

        using var verify = this.factory.Services.CreateScope();
        var item = verify.ServiceProvider.GetRequiredService<DataContext>().ShoppingListItems
            .Include(i => i.Unit).Single(i => i.Id == itemId);
        Assert.Equal(250, item.Amount);
        Assert.Equal("ml", item.Unit?.ShortName);
    }

    [Fact]
    public async Task UnitSearch_OffersOnlyNamedOrFrequentlyUsedUnits()
    {
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            db.Units.Add(new Unit { ShortName = "zqnamed", Name = "Zeta Named" });
            db.Units.Add(new Unit { ShortName = "zqone", Name = "zqone" });
            await db.SaveChangesAsync();
            var freq = new Unit { ShortName = "zqfrq", Name = "zqfrq" };
            db.Units.Add(freq);
            await db.SaveChangesAsync();
            for (var i = 0; i < 3; i++)
                db.ShoppingListItems.Add(new ShoppingListItem
                {
                    ShoppingListId = this.shoppingListId, Name = $"Frq{i}", NormalizedName = $"FRQ{i}",
                    ShortName = "FQ", UnitId = freq.Id, LastChangeByUserId = Constants.SYSUSER_ID,
                });
            await db.SaveChangesAsync();
        }

        using var client = this.CreateClient();
        var units = await client.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("api/Unit/search?pattern=zq");
        var names = units!.Select(u => u.GetProperty("shortName").GetString()).ToList();
        Assert.Contains("zqnamed", names);
        Assert.Contains("zqfrq", names);
        Assert.DoesNotContain("zqone", names);
    }
}
