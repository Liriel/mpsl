using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using mps.Infrastructure;
using mps.Model;
using Xunit;

namespace Tests;

/// <summary>
/// F1: every data endpoint must enforce the "ApprovedUser" policy on the server, not just in the
/// Angular AuthGuard. For every protected surface: anonymous -&gt; 401, an authenticated account with
/// no roles -&gt; 403, an approved ("user") account -&gt; success.
/// </summary>
public class AuthorizationTests : IClassFixture<TestAppFactory>, IAsyncLifetime
{
    private readonly TestAppFactory factory;
    private string approvedUserId = null!;
    private string noRoleUserId = null!;
    private int shoppingListId;
    private int itemId;
    private int unitId;

    public AuthorizationTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = this.factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        this.approvedUserId = await CreateUserAsync(userManager, $"approved-{Guid.NewGuid():N}@test.local");
        this.noRoleUserId = await CreateUserAsync(userManager, $"norole-{Guid.NewGuid():N}@test.local");

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        this.unitId = db.Units.First().Id;

        var list = new ShoppingList { Name = "Auth Test List" };
        db.ShoppingLists.Add(list);
        await db.SaveChangesAsync();
        this.shoppingListId = list.Id;

        var item = new ShoppingListItem
        {
            ShoppingListId = list.Id,
            Name = "Milk",
            NormalizedName = "MILK",
            ShortName = "Mi",
            Status = ItemState.Open,
            AddDate = DateTime.UtcNow,
            // items created through the app always go through PersistedRepository.SaveChanges,
            // which stamps this - set it here too so AutoMapper's ProjectTo (which flattens
            // LastChangeByUser.ShowShortnameInList, a non-nullable bool) doesn't choke on a null nav.
            LastChangeByUserId = Constants.SYSUSER_ID,
        };
        db.ShoppingListItems.Add(item);
        await db.SaveChangesAsync();
        this.itemId = item.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<string> CreateUserAsync(UserManager<ApplicationUser> userManager, string userName)
    {
        var user = new ApplicationUser { UserName = userName, Email = userName };
        var result = await userManager.CreateAsync(user);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        return user.Id;
    }

    private async Task<int> CreateShoppingListAsync(string name)
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var list = new ShoppingList { Name = name };
        db.ShoppingLists.Add(list);
        await db.SaveChangesAsync();
        return list.Id;
    }

    private async Task<int> CreateShoppingListItemAsync(string name)
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var item = new ShoppingListItem
        {
            ShoppingListId = this.shoppingListId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ShortName = name.Length >= 2 ? name.Substring(0, 2) : name,
            Status = ItemState.Open,
            AddDate = DateTime.UtcNow,
            LastChangeByUserId = Constants.SYSUSER_ID,
        };
        db.ShoppingListItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task<int> CreateUnitAsync(string shortName)
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var unit = new Unit { ShortName = shortName, Name = shortName };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        return unit.Id;
    }

    private HttpClient CreateClient(string? userId = null, string? roles = null)
    {
        var client = this.factory.CreateClient();
        if (userId != null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
        if (roles != null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        return client;
    }

    /// <summary>
    /// Sends the request built by <paramref name="requestFactory"/> as anonymous (expect 401),
    /// as a no-role authenticated account (expect 403), and as an approved "user" account
    /// (expect success). A fresh <see cref="HttpRequestMessage"/> is required per send.
    /// </summary>
    private async Task AssertRequiresApprovedUser(Func<HttpRequestMessage> requestFactory)
    {
        using (var anon = this.factory.CreateClient())
        using (var response = await anon.SendAsync(requestFactory()))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using (var noRole = this.CreateClient(this.noRoleUserId))
        using (var response = await noRole.SendAsync(requestFactory()))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using (var approved = this.CreateClient(this.approvedUserId, "user"))
        using (var response = await approved.SendAsync(requestFactory()))
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"expected success, got {response.StatusCode}: {body}");
        }
    }

    // ---- ShoppingList (generic CRUD via EntityController) ----

    [Fact]
    public Task ShoppingList_Index_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, "api/ShoppingList"));

    [Fact]
    public Task ShoppingList_Get_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, $"api/ShoppingList/{this.shoppingListId}"));

    [Fact]
    public Task ShoppingList_Post_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Post, "api/ShoppingList")
        {
            Content = JsonContent.Create(new { Name = $"Posted List {Guid.NewGuid():N}" })
        });

    [Fact]
    public Task ShoppingList_Put_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Put, $"api/ShoppingList/{this.shoppingListId}")
        {
            Content = JsonContent.Create(new { Id = this.shoppingListId, Name = "Auth Test List (updated)" })
        });

    [Fact]
    public async Task ShoppingList_Delete_RequiresApprovedUser()
    {
        // a successful call deletes the list, so each scenario gets its own
        int listForNoRole = await this.CreateShoppingListAsync($"delete-norole-{Guid.NewGuid():N}");
        int listForApproved = await this.CreateShoppingListAsync($"delete-approved-{Guid.NewGuid():N}");

        using (var anon = this.factory.CreateClient())
        using (var response = await anon.DeleteAsync($"api/ShoppingList/{this.shoppingListId}"))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using (var noRole = this.CreateClient(this.noRoleUserId))
        using (var response = await noRole.DeleteAsync($"api/ShoppingList/{listForNoRole}"))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using (var approved = this.CreateClient(this.approvedUserId, "user"))
        using (var response = await approved.DeleteAsync($"api/ShoppingList/{listForApproved}"))
            Assert.True(response.IsSuccessStatusCode, $"got {response.StatusCode}");
    }

    // ---- ShoppingList custom actions ----

    [Fact]
    public Task ShoppingList_Item_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, $"api/ShoppingList/{this.shoppingListId}/item"));

    [Fact]
    public Task ShoppingList_Search_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, $"api/ShoppingList/{this.shoppingListId}/search?pattern=milk"));

    [Fact]
    public Task ShoppingList_Recommendations_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, $"api/ShoppingList/{this.shoppingListId}/recommendations"));

    [Fact]
    public Task ShoppingList_Declutter_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, $"api/ShoppingList/{this.shoppingListId}/declutter"));

    [Fact]
    public Task ShoppingList_AddItem_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Post, $"api/ShoppingList/{this.shoppingListId}/add")
        {
            Content = JsonContent.Create(new { Name = $"Bread {Guid.NewGuid():N}" })
        });

    [Fact]
    public async Task ShoppingList_RemoveItem_RequiresApprovedUser()
    {
        int itemForNoRole = await this.CreateShoppingListItemAsync($"remove-norole-{Guid.NewGuid():N}");
        int itemForApproved = await this.CreateShoppingListItemAsync($"remove-approved-{Guid.NewGuid():N}");

        using (var anon = this.factory.CreateClient())
        using (var response = await anon.DeleteAsync($"api/ShoppingList/{this.shoppingListId}/remove/{this.itemId}"))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using (var noRole = this.CreateClient(this.noRoleUserId))
        using (var response = await noRole.DeleteAsync($"api/ShoppingList/{this.shoppingListId}/remove/{itemForNoRole}"))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using (var approved = this.CreateClient(this.approvedUserId, "user"))
        using (var response = await approved.DeleteAsync($"api/ShoppingList/{this.shoppingListId}/remove/{itemForApproved}"))
            Assert.True(response.IsSuccessStatusCode, $"got {response.StatusCode}");
    }

    [Fact]
    public async Task ShoppingList_BulkDeleteItems_RequiresApprovedUser()
    {
        int itemForApproved = await this.CreateShoppingListItemAsync($"bulk-approved-{Guid.NewGuid():N}");

        await this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Post, $"api/ShoppingList/{this.shoppingListId}/declutter")
        {
            Content = JsonContent.Create(new[] { itemForApproved })
        });
    }

    // ---- Item (generic CRUD via EntityController) ----

    [Fact]
    public Task Item_Index_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, "api/Item"));

    [Fact]
    public Task Item_Get_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, $"api/Item/{this.itemId}"));

    [Fact]
    public async Task Item_Post_RequiresApprovedUser()
    {
        // NOTE: MyProfile ignores ShoppingListId when mapping ShoppingListItemEditViewModel onto a
        // *new* ShoppingListItem (AutomapperConfiguration.cs), so POST api/Item can never actually
        // create a valid item - it always 400s on "Invalid shopping list" regardless of caller.
        // That is a pre-existing, unrelated bug (items are meant to be created through
        // ShoppingListController.AddItem instead); noted for the report, not fixed here. This test
        // only asserts the authorization boundary: anonymous/no-role must still be rejected before
        // that bug is ever reached, and an approved user must be let past the auth gate.
        var body = new { ShoppingListId = this.shoppingListId, Name = $"Posted Item {Guid.NewGuid():N}" };

        using (var anon = this.factory.CreateClient())
        using (var response = await anon.PostAsJsonAsync("api/Item", body))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using (var noRole = this.CreateClient(this.noRoleUserId))
        using (var response = await noRole.PostAsJsonAsync("api/Item", body))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using (var approved = this.CreateClient(this.approvedUserId, "user"))
        using (var response = await approved.PostAsJsonAsync("api/Item", body))
        {
            // not blocked by authorization - reaches the (separately broken) validation logic
            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public Task Item_Put_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Put, $"api/Item/{this.itemId}")
        {
            // ShortName is [Required] on the entity but not on the edit view model, so it must be
            // supplied here - otherwise a successful mapping would null it out and fail to save.
            Content = JsonContent.Create(new { Id = this.itemId, ShoppingListId = this.shoppingListId, Name = "Milk", ShortName = "Mi" })
        });

    [Fact]
    public async Task Item_Delete_RequiresApprovedUser()
    {
        int itemForNoRole = await this.CreateShoppingListItemAsync($"item-delete-norole-{Guid.NewGuid():N}");
        int itemForApproved = await this.CreateShoppingListItemAsync($"item-delete-approved-{Guid.NewGuid():N}");

        using (var anon = this.factory.CreateClient())
        using (var response = await anon.DeleteAsync($"api/Item/{this.itemId}"))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using (var noRole = this.CreateClient(this.noRoleUserId))
        using (var response = await noRole.DeleteAsync($"api/Item/{itemForNoRole}"))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using (var approved = this.CreateClient(this.approvedUserId, "user"))
        using (var response = await approved.DeleteAsync($"api/Item/{itemForApproved}"))
            Assert.True(response.IsSuccessStatusCode, $"got {response.StatusCode}");
    }

    [Fact]
    public async Task Item_Toggle_RequiresApprovedUser()
    {
        int toggleItemId = await this.CreateShoppingListItemAsync($"toggle-{Guid.NewGuid():N}");
        await this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Put, $"api/Item/{toggleItemId}/toggle"));
    }

    // ---- Unit (generic CRUD via EntityController) ----

    [Fact]
    public Task Unit_Index_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, "api/Unit"));

    [Fact]
    public Task Unit_Get_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, $"api/Unit/{this.unitId}"));

    [Fact]
    public Task Unit_Search_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, "api/Unit/search?pattern=g"));

    [Fact]
    public Task Unit_Post_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Post, "api/Unit")
        {
            Content = JsonContent.Create(new { ShortName = Guid.NewGuid().ToString("N").Substring(0, 5), Name = "Test Unit" })
        });

    [Fact]
    public Task Unit_Put_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Put, $"api/Unit/{this.unitId}")
        {
            Content = JsonContent.Create(new { Id = this.unitId, ShortName = "Stk", Name = "Stück" })
        });

    [Fact]
    public async Task Unit_Delete_RequiresApprovedUser()
    {
        int unitForNoRole = await this.CreateUnitAsync(Guid.NewGuid().ToString("N").Substring(0, 5));
        int unitForApproved = await this.CreateUnitAsync(Guid.NewGuid().ToString("N").Substring(0, 5));

        using (var anon = this.factory.CreateClient())
        using (var response = await anon.DeleteAsync($"api/Unit/{this.unitId}"))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using (var noRole = this.CreateClient(this.noRoleUserId))
        using (var response = await noRole.DeleteAsync($"api/Unit/{unitForNoRole}"))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using (var approved = this.CreateClient(this.approvedUserId, "user"))
        using (var response = await approved.DeleteAsync($"api/Unit/{unitForApproved}"))
            Assert.True(response.IsSuccessStatusCode, $"got {response.StatusCode}");
    }

    // ---- PushSubscription ----

    [Fact]
    public Task PushSubscription_VapidPublicKey_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, "api/PushSubscription/vapid-public-key"));

    [Fact]
    public Task PushSubscription_Subscribe_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Post, "api/PushSubscription/subscribe")
        {
            Content = JsonContent.Create(new
            {
                Endpoint = $"https://push.example.com/{Guid.NewGuid():N}",
                P256dh = "test-p256dh-key",
                Auth = "test-auth-secret"
            })
        });

    [Fact]
    public Task PushSubscription_MySubscriptions_RequiresApprovedUser() =>
        this.AssertRequiresApprovedUser(() => new HttpRequestMessage(HttpMethod.Get, "api/PushSubscription/my-subscriptions"));

    // ---- AccountController stays as-is: sign-in surfaces are anonymous, Claim stays bare [Authorize] ----

    [Fact]
    public async Task Account_Index_StaysAnonymous()
    {
        using var anon = this.factory.CreateClient();
        var response = await anon.GetAsync("Account");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task Account_Claim_WorksForFreshlyCreatedRoleLessUser()
    {
        // "GetUserCount() == 1" inside ClaimAdmin means this must be the *only* real user in the
        // database (besides the seeded SYSTEM account) - use a dedicated factory/db so this
        // doesn't collide with the shared fixture's own test users.
        using var freshFactory = new TestAppFactory();
        using var scope = freshFactory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser { UserName = "first-ever@test.local", Email = "first-ever@test.local" };
        var result = await userManager.CreateAsync(user);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));

        var client = freshFactory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user.Id);
        // deliberately no X-Test-Roles header - this mirrors a freshly registered, unapproved account

        var response = await client.GetAsync("Account/Claim");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"expected success, got {response.StatusCode}: {body}");
    }
}
