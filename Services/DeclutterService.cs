using mps.Model;

namespace mps.Services
{
    /// <summary>
    /// Result object for declutter analysis containing item details and purchase statistics.
    /// </summary>
    public class DeclutterCandidate
    {
        public ShoppingListItem Item { get; set; }
        public int PurchaseCount { get; set; }
        public DateTime? LastPurchaseDate { get; set; }
        public int? DaysSinceLastPurchase { get; set; }
    }

    /// <summary>
    /// Service for analyzing shopping list items to identify de-clutter candidates.
    /// De-clutter candidates are items that have been purchased rarely and/or 
    /// haven't been purchased in a long time. Items must be at least 30 days old
    /// to be considered (prevents newly added items from appearing as clutter).
    /// </summary>
    public interface IDeclutterService
    {
        /// <summary>
        /// Get items that are candidates for de-cluttering based on purchase frequency and staleness.
        /// </summary>
        /// <param name="shoppingListId">The shopping list to analyze.</param>
        /// <param name="minMonthsSinceLastPurchase">Minimum months since last purchase (default: 6).</param>
        /// <param name="maxPurchaseCount">Maximum purchase count to be considered rarely purchased (default: 2).</param>
        /// <returns>List of declutter candidates with purchase statistics.</returns>
        IEnumerable<DeclutterCandidate> GetDeclutterCandidates(
            int shoppingListId, 
            int minMonthsSinceLastPurchase = 6, 
            int maxPurchaseCount = 2);

        /// <summary>
        /// Delete multiple shopping list items by their IDs.
        /// </summary>
        /// <param name="shoppingListId">The shopping list that owns the items.</param>
        /// <param name="itemIds">Array of item IDs to delete.</param>
        /// <returns>Number of items deleted.</returns>
        int BulkDeleteItems(int shoppingListId, int[] itemIds);
    }

    public class DeclutterService : IDeclutterService
    {
        private readonly IRepository repo;
        private readonly IEntityService<ShoppingListItem> itemService;

        public DeclutterService(IRepository repo, IEntityService<ShoppingListItem> itemService)
        {
            this.repo = repo;
            this.itemService = itemService;
        }

        public IEnumerable<DeclutterCandidate> GetDeclutterCandidates(
            int shoppingListId, 
            int minMonthsSinceLastPurchase = 6, 
            int maxPurchaseCount = 2)
        {
            var now = DateTime.Now;
            var minDate = now.AddMonths(-minMonthsSinceLastPurchase);

            // Query all items for the shopping list with their history
            var items = from item in this.repo.ShoppingListItems
                        where item.ShoppingListId == shoppingListId
                        select new
                        {
                            Item = item,
                            // Get history records for this item
                            HistoryRecords = item.History
                        };

            // Calculate purchase statistics for each item
            var candidates = items.AsEnumerable()
                .Select(x => new
                {
                    x.Item,
                    PurchaseCount = x.HistoryRecords.Count(),
                    LastPurchaseDate = x.HistoryRecords.Any() 
                        ? x.HistoryRecords.Max(h => h.CheckDate) 
                        : (DateTime?)null,
                    ItemAge = (now - x.Item.AddDate).TotalDays
                })
                .Select(x => new DeclutterCandidate
                {
                    Item = x.Item,
                    PurchaseCount = x.PurchaseCount,
                    LastPurchaseDate = x.LastPurchaseDate,
                    DaysSinceLastPurchase = x.LastPurchaseDate.HasValue 
                        ? (int)(now - x.LastPurchaseDate.Value).TotalDays 
                        : (int?)null
                })
                .Where(x => 
                {
                    // Calculate item age in days
                    var itemAgeDays = (now - x.Item.AddDate).TotalDays;
                    
                    // Only include items that are at least 30 days old (1 month)
                    // This prevents newly added items from showing up as clutter
                    if (itemAgeDays < 30)
                        return false;
                    
                    // Include if any of these conditions are met:
                    return (
                        // Never purchased (but old enough)
                        (x.PurchaseCount == 0) ||
                        // Or rarely purchased
                        (x.PurchaseCount <= maxPurchaseCount) ||
                        // Or not purchased recently
                        (x.LastPurchaseDate.HasValue && x.LastPurchaseDate.Value < minDate)
                    );
                })
                .OrderBy(x => x.LastPurchaseDate ?? DateTime.MinValue)
                .ThenBy(x => x.PurchaseCount)
                .ToList();

            return candidates;
        }

        public int BulkDeleteItems(int shoppingListId, int[] itemIds)
        {
            if (itemIds == null || itemIds.Length == 0)
                return 0;

            // Verify all items belong to the specified shopping list
            var itemsToDelete = this.repo.ShoppingListItems
                .Where(i => i.ShoppingListId == shoppingListId && itemIds.Contains(i.Id))
                .ToList();

            int count = 0;
            foreach (var item in itemsToDelete)
            {
                // Use the entity service to properly handle deletion and notifications
                var result = this.itemService.Delete(item);
                if (result.Success)
                    count++;
            }

            if (count > 0)
                this.repo.SaveChanges();

            return count;
        }
    }
}
