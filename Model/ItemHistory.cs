using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace mps.Model
{
    public class ItemHistory
    {
        [Key]
        public int Id { get; set; }

        public int ShoppingListItemId { get; set; }

        [ForeignKey(nameof(ShoppingListItemId))]
        public virtual ShoppingListItem ShoppingListItem{ get; set; }

        public DateTime CheckDate { get; set; }

        public int? Amount { get; set; }

        public int? UnitId { get; set; }

        [ForeignKey(nameof(UnitId))]
        public virtual Unit Unit { get; set; }
    }
}