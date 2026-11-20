using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using mps.Infrastructure;
using mps.Model;
using mps.Services;
using Xunit;

namespace Tests;

/// <summary>
/// I4 (match key, near matches, suggest endpoint, fresh-item removal) and I5 (merge, duplicate groups).
/// </summary>
public class MatchAndMergeTests : IClassFixture<TestAppFactory>, IAsyncLifetime
{
    private readonly TestAppFactory factory;
    private string userId = null!;
    private int listId;

    public MatchAndMergeTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = this.factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userName = $"m-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser { UserName = userName, Email = userName };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        this.userId = user.Id;

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        this.listId = await NewListAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<int> NewListAsync(DataContext db)
    {
        var list = new ShoppingList { Name = $"M List {Guid.NewGuid():N}" };
        db.ShoppingLists.Add(list);
        await db.SaveChangesAsync();
        return list.Id;
    }

    private HttpClient CreateClient(string? roles = "user")
    {
        var client = this.factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, this.userId);
        if (roles != null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        return client;
    }

    private async Task<int> SeedAsync(string name, int? list = null, ItemState status = ItemState.Archived,
        int purchases = 0, DateTime? addDate = null, params DateTime[] checkDates)
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var item = new ShoppingListItem
        {
            ShoppingListId = list ?? this.listId,
            Name = name,
            NormalizedName = NameHelper.GetNormalizedName(name),
            ShortName = Guid.NewGuid().ToString("N")[..3],
            Status = status,
            AddDate = addDate ?? DateTime.UtcNow.AddDays(-90),
            LastChangeByUserId = Constants.SYSUSER_ID,
        };
        db.ShoppingListItems.Add(item);
        await db.SaveChangesAsync();

        var dates = checkDates.Length > 0
            ? checkDates
            : Enumerable.Range(1, purchases).Select(n => DateTime.UtcNow.AddDays(-n * 7)).ToArray();
        foreach (var d in dates)
            db.ItemHistories.Add(new ItemHistory { ShoppingListItemId = item.Id, CheckDate = d });
        await db.SaveChangesAsync();
        return item.Id;
    }

    // ---- the match key and the near-match rules ----

    [Theory]
    [InlineData("Weißwein", "Weisswein")]
    [InlineData("Weißwein", "Weiszwein")]
    [InlineData("Wiener wurst", "Wienerwurst")]
    [InlineData("Pass Tomaten", "PassTomaten")]
    [InlineData("Käse", "KAESE")]
    [InlineData("Jogurt, 1kg", "jogurt 1kg")]
    public void MatchKey_CollapsesVariants(string a, string b) =>
        Assert.Equal(NameHelper.GetMatchKey(a), NameHelper.GetMatchKey(b));

    [Fact]
    public void MatchKey_DoesNotChangeNormalizedName() =>
        Assert.Equal("WEISZWEIN", NameHelper.GetNormalizedName("Weißwein"));

    [Theory]
    [InlineData("Mozarela", "Mozzarella")]
    [InlineData("Banane", "Bananen")]
    [InlineData("Semmel", "Semmeln")]
    [InlineData("Schampoo", "Shampoo")]
    [InlineData("Weißwein", "Weisswein")]
    public void AreNear_TrueForTyposAndPlurals(string a, string b) =>
        Assert.True(ItemMatchService.AreNear(NameHelper.GetMatchKey(a), NameHelper.GetMatchKey(b)));

    [Theory]
    [InlineData("Wein", "Portwein")]
    [InlineData("Milch", "Milchreis")]
    [InlineData("Pizza 1", "Pizza 2")]
    [InlineData("Tee", "Tea")]
    [InlineData("Brot", "Brei")]
    public void AreNear_FalseForDifferentThings(string a, string b) =>
        Assert.False(ItemMatchService.AreNear(NameHelper.GetMatchKey(a), NameHelper.GetMatchKey(b)));

    // ---- suggest / search ----

    private async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task Suggest_FindsMozzarellaForMozarela_RankedByPurchaseCount()
    {
        await this.SeedAsync("Mozzarella", purchases: 5);
        await this.SeedAsync("Mozarella", purchases: 1);
        await this.SeedAsync("Wein", purchases: 9);
        await this.SeedAsync("Portwein", purchases: 9);

        using var client = this.CreateClient();
        var names = (await this.GetJsonAsync(client, $"api/ShoppingList/{this.listId}/suggest?name=Mozarela"))
            .EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();

        Assert.Equal(new[] { "Mozzarella", "Mozarella" }, names);
    }

    [Fact]
    public async Task Suggest_IsEmptyWhenTheNameMatchesExactly_AndForUnrelatedNames()
    {
        await this.SeedAsync("Mozzarella", purchases: 2);
        using var client = this.CreateClient();

        Assert.Empty((await this.GetJsonAsync(client, $"api/ShoppingList/{this.listId}/suggest?name=mozzarella")).EnumerateArray());
        Assert.Empty((await this.GetJsonAsync(client, $"api/ShoppingList/{this.listId}/suggest?name=Zahnpasta")).EnumerateArray());
    }

    [Fact]
    public async Task Suggest_DoesNotOfferWeinForPortwein()
    {
        await this.SeedAsync("Wein", purchases: 3);
        using var client = this.CreateClient();
        Assert.Empty((await this.GetJsonAsync(client, $"api/ShoppingList/{this.listId}/suggest?name=Portwein")).EnumerateArray());
    }

    [Fact]
    public async Task Suggest_ReturnsAtMostThree()
    {
        foreach (var n in new[] { "Bananen", "Bananan", "Banane", "Bananee" })
            await this.SeedAsync(n, purchases: 1);
        using var client = this.CreateClient();
        var result = await this.GetJsonAsync(client, $"api/ShoppingList/{this.listId}/suggest?name=Banano");
        Assert.True(result.GetArrayLength() <= 3 && result.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Suggest_ForbiddenWithoutApproval_AndForeignListIsScoped()
    {
        using var noRole = this.CreateClient(roles: null);
        using var response = await noRole.GetAsync($"api/ShoppingList/{this.listId}/suggest?name=x");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        int otherList;
        using (var scope = this.factory.Services.CreateScope())
            otherList = await NewListAsync(scope.ServiceProvider.GetRequiredService<DataContext>());
        await this.SeedAsync("Mozzarella", list: otherList, purchases: 4);

        using var client = this.CreateClient();
        Assert.Empty((await this.GetJsonAsync(client, $"api/ShoppingList/{this.listId}/suggest?name=Mozarela")).EnumerateArray());
    }

    [Fact]
    public async Task Search_FindsSpacedNamesByUnspacedPattern()
    {
        await this.SeedAsync("Pass Tomaten", purchases: 1);
        await this.SeedAsync("Weißwein", purchases: 1);
        using var client = this.CreateClient();

        var found = (await this.GetJsonAsync(client, $"api/ShoppingList/{this.listId}/search?pattern=passtomaten"))
            .EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();
        Assert.Equal(new[] { "Pass Tomaten" }, found);

        var wine = (await this.GetJsonAsync(client, $"api/ShoppingList/{this.listId}/search?pattern=weisswein"))
            .EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();
        Assert.Equal(new[] { "Weißwein" }, wine);
    }

    // ---- RemoveItem ----

    [Fact]
    public async Task RemoveItem_FreshWithoutHistory_IsDeleted()
    {
        var id = await this.SeedAsync("Würsrl", status: ItemState.Open, addDate: DateTime.UtcNow.AddMinutes(-2));
        using var client = this.CreateClient();
        using var response = await client.DeleteAsync($"api/ShoppingList/{this.listId}/remove/{id}");
        response.EnsureSuccessStatusCode();

        using var scope = this.factory.Services.CreateScope();
        Assert.Null(scope.ServiceProvider.GetRequiredService<DataContext>().ShoppingListItems.Find(id));
    }

    [Fact]
    public async Task RemoveItem_OldWithoutHistory_IsArchived()
    {
        var id = await this.SeedAsync("Alt", status: ItemState.Open, addDate: DateTime.UtcNow.AddHours(-2));
        using var client = this.CreateClient();
        (await client.DeleteAsync($"api/ShoppingList/{this.listId}/remove/{id}")).EnsureSuccessStatusCode();

        using var scope = this.factory.Services.CreateScope();
        var item = scope.ServiceProvider.GetRequiredService<DataContext>().ShoppingListItems.Find(id);
        Assert.Equal(ItemState.Archived, item!.Status);
    }

    [Fact]
    public async Task RemoveItem_FreshWithHistory_IsArchived()
    {
        var id = await this.SeedAsync("Bekannt", status: ItemState.Open, purchases: 1, addDate: DateTime.UtcNow.AddMinutes(-2));
        using var client = this.CreateClient();
        (await client.DeleteAsync($"api/ShoppingList/{this.listId}/remove/{id}")).EnsureSuccessStatusCode();

        using var scope = this.factory.Services.CreateScope();
        var item = scope.ServiceProvider.GetRequiredService<DataContext>().ShoppingListItems.Find(id);
        Assert.Equal(ItemState.Archived, item!.Status);
    }

    // ---- merge ----

    private Task<HttpResponseMessage> MergeAsync(HttpClient client, int targetId, params int[] sources) =>
        client.PostAsJsonAsync($"api/ShoppingList/{this.listId}/merge", new { TargetId = targetId, SourceIds = sources });

    [Fact]
    public async Task Merge_MovesHistory_SkipsCollidingDates_AndDeletesSources()
    {
        var same = DateTime.UtcNow.AddDays(-10);
        var target = await this.SeedAsync("Mozzarella", checkDates: new[] { same, DateTime.UtcNow.AddDays(-20) });
        var src1 = await this.SeedAsync("Mozarella", checkDates: new[] { same, DateTime.UtcNow.AddDays(-30) });
        var src2 = await this.SeedAsync("Mozzarela", checkDates: new[] { DateTime.UtcNow.AddDays(-40) });

        using var client = this.CreateClient();
        using var response = await this.MergeAsync(client, target, src1, src2);
        response.EnsureSuccessStatusCode();

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(4, db.ItemHistories.Count(h => h.ShoppingListItemId == target));
        Assert.Null(db.ShoppingListItems.Find(src1));
        Assert.Null(db.ShoppingListItems.Find(src2));
        Assert.Equal("Mozzarella", db.ShoppingListItems.Find(target)!.Name);
    }

    [Fact]
    public async Task Merge_MostActiveStatusWins_AndHintIsCarriedOver()
    {
        var target = await this.SeedAsync("Milch", status: ItemState.Archived, purchases: 1);
        var source = await this.SeedAsync("Mlich", status: ItemState.Open, purchases: 1);
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var s = db.ShoppingListItems.Find(source)!;
            s.Amount = 2;
            s.Hint = "Bio";
            await db.SaveChangesAsync();
        }

        using var client = this.CreateClient();
        (await this.MergeAsync(client, target, source)).EnsureSuccessStatusCode();

        using var verify = this.factory.Services.CreateScope();
        var item = verify.ServiceProvider.GetRequiredService<DataContext>().ShoppingListItems.Find(target)!;
        Assert.Equal(ItemState.Open, item.Status);
        Assert.Equal(2, item.Amount);
        Assert.Equal("Bio", item.Hint);
        Assert.Equal("Milch", item.Name);
    }

    [Fact]
    public async Task Merge_Returns400ForForeignListIdOrTargetInSources()
    {
        int otherList;
        using (var scope = this.factory.Services.CreateScope())
            otherList = await NewListAsync(scope.ServiceProvider.GetRequiredService<DataContext>());
        var target = await this.SeedAsync("A-Item");
        var foreign = await this.SeedAsync("B-Item", list: otherList);

        using var client = this.CreateClient();
        using var cross = await this.MergeAsync(client, target, foreign);
        Assert.Equal(HttpStatusCode.BadRequest, cross.StatusCode);
        var body = JsonDocument.Parse(await cross.Content.ReadAsStringAsync()).RootElement;
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.True(body.GetProperty("validationErrors").GetArrayLength() > 0);

        using var self = await this.MergeAsync(client, target, target);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);

        using var none = await this.MergeAsync(client, target);
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);

        using var verify = this.factory.Services.CreateScope();
        Assert.NotNull(verify.ServiceProvider.GetRequiredService<DataContext>().ShoppingListItems.Find(foreign));
    }

    [Fact]
    public async Task Merge_Returns403WithoutApproval()
    {
        var target = await this.SeedAsync("A-Item");
        var source = await this.SeedAsync("A-Itme");
        using var client = this.CreateClient(roles: null);
        using var response = await this.MergeAsync(client, target, source);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- duplicates ----

    [Fact]
    public async Task Duplicates_GroupsVariants_BestEstablishedFirst_AndLeavesTheRestAlone()
    {
        await this.SeedAsync("Weisswein", purchases: 8);
        await this.SeedAsync("Weißwein", purchases: 2);
        await this.SeedAsync("Wein", purchases: 5);
        await this.SeedAsync("Portwein", purchases: 5);
        await this.SeedAsync("Bananen", purchases: 9);
        await this.SeedAsync("Banane", purchases: 1);
        await this.SeedAsync("Butter", purchases: 3);

        using var client = this.CreateClient();
        var groups = (await this.GetJsonAsync(client, $"api/ShoppingList/{this.listId}/duplicates"))
            .EnumerateArray()
            .Select(g => g.EnumerateArray().Select(i => i.GetProperty("name").GetString()!).ToList())
            .ToList();

        Assert.Equal(2, groups.Count);
        Assert.Contains(new[] { "Bananen", "Banane" }, groups, new SequenceComparer());
        Assert.Contains(new[] { "Weisswein", "Weißwein" }, groups, new SequenceComparer());
    }

    [Fact]
    public async Task Duplicates_CapsTransitiveChains()
    {
        // each neighbour is one edit apart, so all of them chain into one component
        var names = new[] { "Marmelade1", "Marmelade2", "Marmelada2", "Marmelaca2", "Marmelaci2", "Marmelaki2", "Marmelaku2", "Marmelakuu" };
        foreach (var n in names)
            await this.SeedAsync(n, purchases: 1);

        using var scope = this.factory.Services.CreateScope();
        var groups = scope.ServiceProvider.GetRequiredService<IItemMatchService>().FindDuplicateGroups(this.listId);
        Assert.All(groups, g => Assert.True(g.Items.Count <= ItemMatchService.MaxGroupSize));
    }

    private sealed class SequenceComparer : IEqualityComparer<IEnumerable<string>>
    {
        public bool Equals(IEnumerable<string>? x, IEnumerable<string>? y) => x!.SequenceEqual(y!);
        public int GetHashCode(IEnumerable<string> obj) => obj.Count();
    }
}
