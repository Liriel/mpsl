using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace mps.Model
{
    /// <summary>
    /// One row per (item, check date): the unique index is the backstop behind
    /// <see cref="mps.Services.IItemArchiveService"/>, so a race can never record a purchase twice.
    /// </summary>
    [Index(nameof(ShoppingListItemId), nameof(CheckDate), IsUnique = true)]
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