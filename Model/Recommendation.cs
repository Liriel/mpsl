using System.ComponentModel.DataAnnotations.Schema;

namespace mps.Model
{
    public class Recommendation{

        public int ShoppingListId { get; set; }

        [ForeignKey(nameof(ShoppingListId))]
        public virtual ShoppingList ShoppingList { get; set; }

        [Column("Id")]
        public int ShoppingListItemId { get; set; }

        [ForeignKey(nameof(ShoppingListItemId))]
        public virtual ShoppingListItem ShoppingListItem { get; set; }

        public int Count { get; set; }

        public DateTime LastCheckDate { get; set; }

        /// <summary>
        /// Average days between checks.
        /// </summary>
        public float AvgDiff { get; set; }

        public float Weight { get; set; }

        public float Rank { get; set; }

    }
}