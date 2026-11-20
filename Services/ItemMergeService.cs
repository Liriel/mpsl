using Microsoft.EntityFrameworkCore;
using mps.Infrastructure;
using mps.Model;

namespace mps.Services;

public interface IItemMergeService
{
    /// <summary>
    /// Folds the <paramref name="sourceIds"/> items into <paramref name="targetId"/>: their purchase
    /// history moves over, the most active status wins and the sources are deleted. Every id must
    /// belong to <paramref name="shoppingListId"/>.
    /// </summary>
    EntityOperationResult<ShoppingListItem> Merge(int shoppingListId, int targetId, IReadOnlyCollection<int> sourceIds);
}

public class ItemMergeService : IItemMergeService
{
    private readonly IRepository repo;
    private readonly DataContext db;
    private readonly IEntityService<ShoppingListItem> itemService;
    private readonly INotificationService notificationService;

    public ItemMergeService(IRepository repo, DataContext db, IEntityService<ShoppingListItem> itemService, INotificationService notificationService)
    {
        this.repo = repo;
        this.db = db;
        this.itemService = itemService;
        this.notificationService = notificationService;
    }

    public EntityOperationResult<ShoppingListItem> Merge(int shoppingListId, int targetId, IReadOnlyCollection<int> sourceIds)
    {
        var distinctSources = sourceIds?.Distinct().ToList() ?? new List<int>();
        if (distinctSources.Count == 0)
            return Fail("Select at least one item to merge into the target.", "sourceIds");

        if (distinctSources.Contains(targetId))
            return Fail("The target cannot also be a source.", "targetId", "sourceIds");

        var ids = distinctSources.Append(targetId).ToList();
        var items = this.repo.ShoppingListItems
            .Where(i => i.ShoppingListId == shoppingListId && ids.Contains(i.Id))
            .ToList();

        if (items.Count != ids.Count)
            return Fail("Every item must belong to this list.", "targetId", "sourceIds");

        var target = items.Single(i => i.Id == targetId);
        var sources = items.Where(i => i.Id != targetId).ToList();

        // ---- history: repoint, skipping rows that would hit the (item, CheckDate) unique index ----
        var taken = this.repo.ItemHistories.Where(h => h.ShoppingListItemId == targetId)
            .Select(h => h.CheckDate).ToHashSet();

        var histories = this.repo.ItemHistories.Where(h => distinctSources.Contains(h.ShoppingListItemId))
            .OrderBy(h => h.CheckDate).ToList();

        foreach (var history in histories)
        {
            if (taken.Add(history.CheckDate))
                history.ShoppingListItemId = targetId;
            else
                this.db.ItemHistories.Remove(history); // same purchase recorded under both names
        }

        // ---- status: most active wins (Open > Checked > Archived); the winner's amount, unit and
        // check date come along because they describe the current request ----
        var winner = items
            .OrderBy(i => i.Status)
            .ThenByDescending(i => i.CheckDate ?? DateTime.MinValue)
            .ThenBy(i => i.Id == targetId ? 0 : 1)
            .First();

        if (winner.Id != targetId)
        {
            target.Status = winner.Status;
            target.CheckDate = winner.CheckDate;
            target.CheckedByUserId = winner.CheckedByUserId;
            if (winner.Amount != null)
            {
                target.Amount = winner.Amount;
                target.UnitId = winner.UnitId;
            }
        }

        if (string.IsNullOrWhiteSpace(target.Hint))
            target.Hint = sources.Select(s => s.Hint).FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

        // The repointed rows must leave the sources' History collections before the sources are
        // removed, or EF's cascade would delete them with their old parent.
        this.db.ChangeTracker.DetectChanges();

        // one save for the history moves, the target and the deletes; broadcasts removals per source
        this.itemService.DeleteRange(sources);

        if (target.Status != ItemState.Archived)
            this.notificationService.BroadCastItemChanged(target);

        return new EntityOperationResult<ShoppingListItem> { Entity = target, Success = true };
    }

    private static EntityOperationResult<ShoppingListItem> Fail(string message, params string[] fields) =>
        new EntityOperationResult<ShoppingListItem>(new ValidationError(message, fields));
}
