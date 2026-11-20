namespace mps.ViewModels
{
    /// <summary>An existing item offered as a "did you mean" suggestion or listed in a duplicate group.</summary>
    public class ItemMatchViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string ShortName { get; set; }

        public string Status { get; set; }

        public int PurchaseCount { get; set; }

        public DateTime? LastPurchaseDate { get; set; }
    }

    /// <summary>Body of <c>POST api/ShoppingList/{id}/merge</c>.</summary>
    public class ItemMergeViewModel
    {
        public int TargetId { get; set; }

        public int[] SourceIds { get; set; } = System.Array.Empty<int>();
    }
}
