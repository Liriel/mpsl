namespace mps.ViewModels
{
    /// <summary>
    /// View model representing an item that is a candidate for de-cluttering.
    /// Items become candidates when they have been purchased rarely and/or 
    /// haven't been purchased in a long time.
    /// </summary>
    public class DeclutterCandidateViewModel
    {
        /// <summary>
        /// The shopping list item ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The shopping list ID this item belongs to.
        /// </summary>
        public int ShoppingListId { get; set; }

        /// <summary>
        /// The item name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The item's short name (avatar).
        /// </summary>
        public string ShortName { get; set; }

        /// <summary>
        /// Total number of times this item has been purchased (history count).
        /// </summary>
        public int PurchaseCount { get; set; }

        /// <summary>
        /// The date this item was last purchased. Null if never purchased.
        /// </summary>
        public DateTime? LastPurchaseDate { get; set; }

        /// <summary>
        /// Number of days since the item was last purchased. Null if never purchased.
        /// </summary>
        public int? DaysSinceLastPurchase { get; set; }

        /// <summary>"Product" or "Task".</summary>
        public string Kind { get; set; }
    }
}
