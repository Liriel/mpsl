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
        /// <summary>
        /// Items younger than this are never declutter candidates, so a freshly added item cannot
        /// immediately look like clutter.
        /// </summary>
        private const int MinimumItemAgeDays = 30;

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
            var now = DateTime.UtcNow;
            var minDate = now.AddMonths(-minMonthsSinceLastPurchase);
            var maxAddDate = now.AddDays(-MinimumItemAgeDays);

            // One grouped query rather than materialising every item and walking History through
            // the lazy loading proxies, which cost a round trip per item.
            var stats = from item in this.repo.ShoppingListItems
                        where item.ShoppingListId == shoppingListId
                        // Only consider items that are at least 30 days old, so newly added items
                        // never show up as clutter.
                        where item.AddDate <= maxAddDate
                        let purchaseCount = item.History.Count()
                        let lastPurchaseDate = item.History
                            .Select(h => (DateTime?)h.CheckDate)
                            .Max()
                        // An item qualifies if ANY criterion matches: rarely purchased, never
                        // purchased, or not purchased for a long time.
                        where purchaseCount <= maxPurchaseCount
                            || lastPurchaseDate == null
                            || lastPurchaseDate < minDate
                        orderby lastPurchaseDate, purchaseCount
                        select new
                        {
                            Item = item,
                            PurchaseCount = purchaseCount,
                            LastPurchaseDate = lastPurchaseDate
                        };

            return stats.AsEnumerable()
                .Select(x => new DeclutterCandidate
                {
                    Item = x.Item,
                    PurchaseCount = x.PurchaseCount,
                    LastPurchaseDate = x.LastPurchaseDate,
                    DaysSinceLastPurchase = x.LastPurchaseDate.HasValue
                        ? (int)(now - x.LastPurchaseDate.Value).TotalDays
                        : (int?)null
                })
                .ToList();
        }

        public int BulkDeleteItems(int shoppingListId, int[] itemIds)
        {
            if (itemIds == null || itemIds.Length == 0)
                return 0;

            // Verify all items belong to the specified shopping list
            var itemsToDelete = this.repo.ShoppingListItems
                .Where(i => i.ShoppingListId == shoppingListId && itemIds.Contains(i.Id))
                .ToList();

            // Use the entity service so deletion notifications still go out, but as a single save
            // rather than one round trip per item.
            return this.itemService.DeleteRange(itemsToDelete);
        }
    }
}
