using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using mps.Infrastructure;
using mps.Model;
using Xunit;

namespace Tests;

/// <summary>
/// I7: the <c>Recommendations</c> view ranks by the overdue ratio since / median interval. The view is
/// raw SQL created by a migration, so these run against the real temp SQLite file of
/// <see cref="TestAppFactory"/>. Order is read the way the controller does: ascending Rank.
/// </summary>
public class RecommendationRankTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory factory;

    public RecommendationRankTests(TestAppFactory factory) => this.factory = factory;

    /// <summary>Seeds an archived item bought every <paramref name="cycleDays"/> days, last bought
    /// <paramref name="daysSinceLast"/> days ago, with <paramref name="purchases"/> purchases.</summary>
    private static void Seed(DataContext db, int listId, string name, double cycleDays, double daysSinceLast, int purchases = 5)
    {
        var now = DateTime.UtcNow;
        var item = new ShoppingListItem
        {
            ShoppingListId = listId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ShortName = name.Substring(0, 2),
            Status = ItemState.Archived,
            AddDate = now.AddDays(-500),
            LastChangeByUserId = Constants.SYSUSER_ID,
        };
        db.ShoppingListItems.Add(item);
        db.SaveChanges();
        for (var k = 0; k < purchases; k++)
            db.ItemHistories.Add(new ItemHistory
            {
                ShoppingListItemId = item.Id,
                CheckDate = now.AddDays(-daysSinceLast - k * cycleDays),
            });
        db.SaveChanges();
    }

    [Fact]
    public void Rank_PrefersDueAndMildlyOverdueOverRecentAndAbsurdlyOverdue()
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var list = new ShoppingList { Name = $"Rank {Guid.NewGuid():N}" };
        db.ShoppingLists.Add(list);
        db.SaveChanges();

        Seed(db, list.Id, "Due", 14, 14);
        Seed(db, list.Id, "Recent", 14, 2);
        Seed(db, list.Id, "Mild", 14, 21);
        Seed(db, list.Id, "Absurd", 14, 140);
        Seed(db, list.Id, "TwoOnly", 14, 14, purchases: 2);
        // one holiday-sized gap must not distort the typical interval (median, not mean)
        Seed(db, list.Id, "Outlier", 7, 7, purchases: 5);
        var outlier = db.ShoppingListItems.Single(i => i.ShoppingListId == list.Id && i.Name == "Outlier");
        var oldest = db.ItemHistories.Where(h => h.ShoppingListItemId == outlier.Id).OrderBy(h => h.CheckDate).First();
        oldest.CheckDate = oldest.CheckDate.AddDays(-200);
        db.SaveChanges();

        var rows = db.Recommendations.Where(r => r.ShoppingListId == list.Id).OrderBy(r => r.Rank)
            .Select(r => new { r.ShoppingListItem.Name, r.Weight, r.Rank }).ToList();
        var names = rows.Select(r => r.Name).ToList();

        Assert.DoesNotContain("TwoOnly", names);
        Assert.Equal(5, names.Count);
        Assert.True(names.IndexOf("Mild") < names.IndexOf("Recent"));
        Assert.True(names.IndexOf("Due") < names.IndexOf("Recent"));
        Assert.True(names.IndexOf("Mild") < names.IndexOf("Absurd"));
        Assert.True(names.IndexOf("Due") < names.IndexOf("Absurd"));

        Assert.InRange(rows.Single(r => r.Name == "Due").Weight, 0.95f, 1.05f);
        Assert.InRange(rows.Single(r => r.Name == "Recent").Weight, 0.1f, 0.2f);
        // median interval stays 7 days despite the 200-day gap (the mean would be ~57)
        Assert.InRange(rows.Single(r => r.Name == "Outlier").Weight, 0.9f, 1.1f);
    }
}
