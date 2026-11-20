using Microsoft.EntityFrameworkCore;
using mps.Model;

namespace mps.Services;

public interface IItemArchiveService
{
    /// <summary>
    /// Archives every item of the list that was checked before <paramref name="cutoff"/> and records
    /// its purchase in the item history. Safe to call concurrently: each item is claimed atomically,
    /// so exactly one caller writes the history row.
    /// </summary>
    /// <returns>The number of items this call archived.</returns>
    int ArchiveStale(int shoppingListId, DateTime cutoff);
}

public class ItemArchiveService : IItemArchiveService
{
    private readonly DataContext db;
    private readonly ILogger<ItemArchiveService> logger;

    public ItemArchiveService(DataContext db, ILogger<ItemArchiveService> logger)
    {
        this.db = db;
        this.logger = logger;
    }

    public int ArchiveStale(int shoppingListId, DateTime cutoff)
    {
        var candidates = this.db.ShoppingListItems.AsNoTracking()
            .Where(i => i.ShoppingListId == shoppingListId
                        && i.Status == ItemState.Checked
                        && i.CheckDate != null && i.CheckDate < cutoff)
            .Select(i => new { i.Id, i.Amount, i.UnitId, CheckDate = i.CheckDate.Value })
            .ToList();

        var archived = 0;
        foreach (var candidate in candidates)
        {
            // The claim and the history row commit together, so a crash cannot leave an archived
            // item without its history. The conditional UPDATE is the lock: a concurrent caller
            // (or a user un-checking and re-checking the item) makes it affect 0 rows, and then
            // this caller writes nothing.
            using var transaction = this.db.Database.BeginTransaction();

            var claimed = this.db.ShoppingListItems
                .Where(i => i.Id == candidate.Id
                            && i.Status == ItemState.Checked
                            && i.CheckDate == candidate.CheckDate)
                .ExecuteUpdate(s => s.SetProperty(i => i.Status, ItemState.Archived));

            if (claimed != 1)
                continue; // disposing the transaction rolls back the no-op

            this.db.ItemHistories.Add(new ItemHistory
            {
                ShoppingListItemId = candidate.Id,
                Amount = candidate.Amount,
                UnitId = candidate.UnitId,
                CheckDate = candidate.CheckDate,
            });
            this.db.SaveChanges();
            transaction.Commit();
            this.db.ChangeTracker.Clear();
            archived++;
        }

        if (archived > 0)
            this.logger.LogInformation("Archived {Count} stale items on list {ListId}", archived, shoppingListId);

        return archived;
    }
}
