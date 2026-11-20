using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using mps.Infrastructure;
using mps.Model;
using mps.Services;
using Xunit;

namespace Tests;

/// <summary>Product vs task: classifier, add/edit endpoints, archiving, recommendations, matching.</summary>
public class ItemKindTests : IClassFixture<TestAppFactory>, IAsyncLifetime
{
    private readonly TestAppFactory factory;
    private string userId = null!;
    private int listId;

    public ItemKindTests(TestAppFactory factory) => this.factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = this.factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userName = $"k-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser { UserName = userName, Email = userName };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        this.userId = user.Id;

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        this.listId = await NewListAsync(db, $"K List {Guid.NewGuid():N}");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<int> NewListAsync(DataContext db, string name)
    {
        var list = new ShoppingList { Name = name };
        db.ShoppingLists.Add(list);
        await db.SaveChangesAsync();
        return list.Id;
    }

    private HttpClient CreateClient()
    {
        var client = this.factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, this.userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "user");
        return client;
    }

    private async Task<int> SeedAsync(string name, ItemKind kind = ItemKind.Product, ItemState status = ItemState.Archived,
        int purchases = 0, DateTime? checkDate = null)
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var item = new ShoppingListItem
        {
            ShoppingListId = this.listId,
            Name = name,
            NormalizedName = NameHelper.GetNormalizedName(name),
            ShortName = Guid.NewGuid().ToString("N")[..3],
            Status = status,
            Kind = kind,
            CheckDate = checkDate,
            AddDate = DateTime.UtcNow.AddDays(-500),
            LastChangeByUserId = Constants.SYSUSER_ID,
        };
        db.ShoppingListItems.Add(item);
        await db.SaveChangesAsync();
        for (var k = 0; k < purchases; k++)
            db.ItemHistories.Add(new ItemHistory { ShoppingListItemId = item.Id, CheckDate = DateTime.UtcNow.AddDays(-14 - k * 14) });
        await db.SaveChangesAsync();
        return item.Id;
    }

    private ShoppingListItem Load(int id)
    {
        using var scope = this.factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DataContext>().ShoppingListItems.AsNoTracking().Single(i => i.Id == id);
    }

    private async Task<JsonElement> AddAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync($"api/ShoppingList/{this.listId}/add", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("entity").Clone();
    }

    // ---- classifier ----

    [Theory]
    [InlineData("Tanken")]
    [InlineData("Kidspacken")]
    [InlineData("Gutscheineinpacken")]
    [InlineData("Hortzeiten")]
    [InlineData("Englishschularbeit")]
    [InlineData("Stundenpläne")]
    [InlineData("Oma anrufen")]
    [InlineData("Paket abholen")]
    [InlineData("Arzttermin")]
    [InlineData("Rechnung zahlen")]
    public void Classifier_Tasks(string name) =>
        Assert.Equal(ItemKind.Task, new ItemKindClassifier().Classify(name, Array.Empty<string>()).Kind);

    [Theory]
    [InlineData("Bananen")]
    [InlineData("Nudeln")]
    [InlineData("Semmeln")]
    [InlineData("Milch")]
    [InlineData("Packerlsuppe")]
    [InlineData("Zahnpasta")]
    [InlineData("Mozzarella")]
    [InlineData("Kartoffeln")]
    [InlineData("Zahnbürsten")]
    public void Classifier_Products(string name) =>
        Assert.Equal(ItemKind.Product, new ItemKindClassifier().Classify(name, new[] { "Apotheke" }).Kind);

    [Theory]
    [InlineData("Apotheke")]
    [InlineData("dm")]
    [InlineData("Bäcker")]
    [InlineData("Gewand  Kinder")]
    public void Classifier_NameOfAnotherList_IsTask(string name) =>
        Assert.Equal(ItemKind.Task, new ItemKindClassifier()
            .Classify(name, new[] { "Apotheke", "DM", "Bäcker", "Gewand Kinder" }).Kind);

    [Fact]
    public void StripTaskPrefix_RemovesBangAndWhitespace()
    {
        var name = "  !  Tanken ";
        Assert.True(ItemKindClassifier.StripTaskPrefix(ref name));
        Assert.Equal("Tanken", name);

        var plain = "Milch";
        Assert.False(ItemKindClassifier.StripTaskPrefix(ref plain));
        Assert.Equal("Milch", plain);
    }

    // ---- add ----

    [Fact]
    public async Task Add_BangPrefix_IsTaskWithStrippedName()
    {
        using var client = this.CreateClient();
        var entity = await this.AddAsync(client, new { Name = "!Tanken" });

        Assert.Equal("Tanken", entity.GetProperty("name").GetString());
        Assert.Equal("Task", entity.GetProperty("kind").GetString());
        Assert.False(entity.GetProperty("kindAuto").GetBoolean());
        Assert.Equal(ItemKind.Task, this.Load(entity.GetProperty("id").GetInt32()).Kind);
    }

    [Fact]
    public async Task Add_ClassifierTask_ReportsAutoAndReason()
    {
        using var client = this.CreateClient();
        var entity = await this.AddAsync(client, new { Name = "Kinder abholen" });

        Assert.Equal("Task", entity.GetProperty("kind").GetString());
        Assert.True(entity.GetProperty("kindAuto").GetBoolean());
        Assert.False(string.IsNullOrEmpty(entity.GetProperty("kindReason").GetString()));
    }

    [Fact]
    public async Task Add_NameOfAnotherList_IsTask()
    {
        using (var scope = this.factory.Services.CreateScope())
            await NewListAsync(scope.ServiceProvider.GetRequiredService<DataContext>(), "Baumarkt Test");

        using var client = this.CreateClient();
        var entity = await this.AddAsync(client, new { Name = "baumarkt test" });
        Assert.Equal("Task", entity.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Add_Product_IsProduct()
    {
        using var client = this.CreateClient();
        var entity = await this.AddAsync(client, new { Name = "Zahnpasta" });
        Assert.Equal("Product", entity.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Add_ExplicitKind_OverridesClassifier()
    {
        using var client = this.CreateClient();
        var product = await this.AddAsync(client, new { Name = "Tanken", Kind = "Product" });
        Assert.Equal("Product", product.GetProperty("kind").GetString());
        Assert.False(product.GetProperty("kindAuto").GetBoolean());

        var task = await this.AddAsync(client, new { Name = "Brot", Kind = "Task" });
        Assert.Equal("Task", task.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task ReAdd_KeepsUserCorrectedKind_UnlessExplicit()
    {
        using var client = this.CreateClient();
        var first = await this.AddAsync(client, new { Name = "Autowaschen und bringen" });
        var id = first.GetProperty("id").GetInt32();
        Assert.Equal(ItemKind.Task, this.Load(id).Kind);

        // the user corrects it to a product
        using var put = await client.PutAsJsonAsync($"api/item/{id}",
            new { Id = id, ShoppingListId = this.listId, Name = "Autowaschen und bringen", Kind = "Product" });
        put.EnsureSuccessStatusCode();

        var again = await this.AddAsync(client, new { Name = "Autowaschen und bringen" });
        Assert.Equal(id, again.GetProperty("id").GetInt32());
        Assert.Equal("Product", again.GetProperty("kind").GetString());
        Assert.False(again.GetProperty("kindAuto").GetBoolean());
        Assert.Equal(ItemKind.Product, this.Load(id).Kind);

        // an explicit "!" flips it again
        await this.AddAsync(client, new { Name = "!Autowaschen und bringen" });
        Assert.Equal(ItemKind.Task, this.Load(id).Kind);
    }

    // ---- edit ----

    [Fact]
    public async Task Put_WithKind_Flips_WithoutKind_LeavesIt()
    {
        var id = await this.SeedAsync("Wäsche");
        using var client = this.CreateClient();

        using (var r = await client.PutAsJsonAsync($"api/item/{id}",
            new { Id = id, ShoppingListId = this.listId, Name = "Wäsche", Kind = "Task" }))
            r.EnsureSuccessStatusCode();
        Assert.Equal(ItemKind.Task, this.Load(id).Kind);

        using (var r = await client.PutAsJsonAsync($"api/item/{id}",
            new { Id = id, ShoppingListId = this.listId, Name = "Wäsche", Hint = "heute" }))
            r.EnsureSuccessStatusCode();
        var item = this.Load(id);
        Assert.Equal(ItemKind.Task, item.Kind);
        Assert.Equal("heute", item.Hint);

        using (var r = await client.PutAsJsonAsync($"api/item/{id}",
            new { Id = id, ShoppingListId = this.listId, Name = "Wäsche", Kind = "Product" }))
            r.EnsureSuccessStatusCode();
        Assert.Equal(ItemKind.Product, this.Load(id).Kind);
    }

    [Fact]
    public async Task Get_SerializesKindAsString()
    {
        await this.SeedAsync("Putzen", ItemKind.Task, ItemState.Open);
        using var client = this.CreateClient();
        var items = await client.GetFromJsonAsync<JsonElement>($"api/ShoppingList/{this.listId}/item");
        Assert.Equal("Task", items.EnumerateArray().Single(i => i.GetProperty("name").GetString() == "Putzen").GetProperty("kind").GetString());
    }

    // ---- archiving ----

    [Fact]
    public async Task Archive_CheckedTask_WritesNoHistory_ProductStill()
    {
        var checkedAt = DateTime.UtcNow.AddHours(-3);
        var task = await this.SeedAsync("Tanken", ItemKind.Task, ItemState.Checked, checkDate: checkedAt);
        var product = await this.SeedAsync("Butter", ItemKind.Product, ItemState.Checked, checkDate: checkedAt);

        using (var scope = this.factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IItemArchiveService>();
            Assert.Equal(2, service.ArchiveStale(this.listId, DateTime.UtcNow.AddHours(-1)));
        }

        using var verify = this.factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(ItemState.Archived, db.ShoppingListItems.Single(i => i.Id == task).Status);
        Assert.Equal(ItemState.Archived, db.ShoppingListItems.Single(i => i.Id == product).Status);
        Assert.Empty(db.ItemHistories.Where(h => h.ShoppingListItemId == task));
        Assert.Single(db.ItemHistories.Where(h => h.ShoppingListItemId == product));
    }

    // ---- exclusions ----

    [Fact]
    public async Task Recommendations_ExcludeTasks()
    {
        var task = await this.SeedAsync("Apotheke", ItemKind.Task, purchases: 5);
        var product = await this.SeedAsync("Nudeln", ItemKind.Product, purchases: 5);

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var names = db.Recommendations.AsNoTracking().Where(r => r.ShoppingListId == this.listId).Select(r => r.ShoppingListItem.Name).ToList();
        Assert.Contains("Nudeln", names);
        Assert.DoesNotContain("Apotheke", names);
        Assert.NotEqual(task, product);
    }

    [Fact]
    public async Task DuplicateGroupsAndSuggestions_IgnoreTasks_ButExactMatchStillCounts()
    {
        await this.SeedAsync("Bananen");
        await this.SeedAsync("Banane", ItemKind.Task);
        await this.SeedAsync("Tanken", ItemKind.Task);
        await this.SeedAsync("Tanke", ItemKind.Product);
        using var client = this.CreateClient();

        var groups = await client.GetFromJsonAsync<JsonElement>($"api/ShoppingList/{this.listId}/duplicates");
        var grouped = groups.EnumerateArray().SelectMany(g => g.EnumerateArray()).Select(i => i.GetProperty("name").GetString()).ToList();
        Assert.DoesNotContain("Banane", grouped);
        Assert.DoesNotContain("Tanken", grouped);

        var suggest = await client.GetFromJsonAsync<JsonElement>($"api/ShoppingList/{this.listId}/suggest?name=Bananan");
        Assert.DoesNotContain("Banane", suggest.EnumerateArray().Select(i => i.GetProperty("name").GetString()));
        Assert.Contains("Bananen", suggest.EnumerateArray().Select(i => i.GetProperty("name").GetString()));

        // an existing task named exactly like the input still silences the suggestion
        var exact = await client.GetFromJsonAsync<JsonElement>($"api/ShoppingList/{this.listId}/suggest?name=Tanken");
        Assert.Empty(exact.EnumerateArray());
    }

    [Fact]
    public async Task Search_StillFindsTasks()
    {
        await this.SeedAsync("Kidspacken", ItemKind.Task);
        using var client = this.CreateClient();
        var found = await client.GetFromJsonAsync<JsonElement>($"api/ShoppingList/{this.listId}/search?pattern=kids");
        Assert.Contains("Kidspacken", found.EnumerateArray().Select(i => i.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Declutter_KeepsTasksAndExposesKind()
    {
        await this.SeedAsync("Hortzeiten", ItemKind.Task);
        using var client = this.CreateClient();
        var candidates = await client.GetFromJsonAsync<JsonElement>($"api/ShoppingList/{this.listId}/declutter");
        Assert.Equal("Task", candidates.EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Hortzeiten").GetProperty("kind").GetString());
    }
}
