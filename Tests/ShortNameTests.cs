using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using mps.Infrastructure;
using mps.Model;
using Xunit;

namespace Tests;

/// <summary>Short-name generation for new items, notably multi-word names whose last word is short.</summary>
public class ShortNameTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory factory;

    public ShortNameTests(TestAppFactory factory) => this.factory = factory;

    [Theory]
    [InlineData("Rote Zora", 1, "RO")]
    [InlineData("Rote Zora", 3, "RA")]
    [InlineData("Rote Zora", 4, "R4")] // past the end of "Zora": numbered fallback, not an exception
    [InlineData("Rote Zora", 12, "R12")]
    [InlineData("Milch", 4, "MH")]
    [InlineData("Milch", 5, "M5")]
    [InlineData("Ei", 1, "EI")]
    [InlineData("Rote  Zora", 2, "RR")] // double space must not yield an empty last word
    public void GetShortName_StaysInBounds(string name, int offset, string expected) =>
        Assert.Equal(expected, NameHelper.GetShortName(name, 2, offset));

    [Fact]
    public async Task AddItem_MultiWordName_WhenShortCodesTaken_Succeeds()
    {
        string userId;
        int listId;
        using (var scope = this.factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var userName = $"sn-{Guid.NewGuid():N}@test.local";
            var user = new ApplicationUser { UserName = userName, Email = userName };
            Assert.True((await userManager.CreateAsync(user)).Succeeded);
            userId = user.Id;

            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var list = new ShoppingList { Name = $"SN {Guid.NewGuid():N}" };
            db.ShoppingLists.Add(list);
            await db.SaveChangesAsync();
            listId = list.Id;

            // occupy every code "Rote Zora" can get from the letters of "Zora"
            foreach (var (name, shortName) in new[] { ("Rosinen", "RO"), ("Rucola", "RR"), ("Radieschen", "RA") })
            {
                db.ShoppingListItems.Add(new ShoppingListItem
                {
                    ShoppingListId = listId,
                    Name = name,
                    NormalizedName = NameHelper.GetNormalizedName(name),
                    ShortName = shortName,
                    Status = ItemState.Archived,
                    AddDate = DateTime.UtcNow.AddDays(-100),
                    LastChangeByUserId = Constants.SYSUSER_ID,
                });
            }
            await db.SaveChangesAsync();
        }

        var client = this.factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "user");

        using var response = await client.PostAsJsonAsync($"api/ShoppingList/{listId}/add",
            new { name = "Rote Zora", amount = 0, unitShortName = "", shoppingListId = listId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var check = this.factory.Services.CreateScope();
        var item = check.ServiceProvider.GetRequiredService<DataContext>().ShoppingListItems
            .Single(i => i.ShoppingListId == listId && i.Name == "Rote Zora");
        Assert.Equal("R4", item.ShortName);
    }
}
