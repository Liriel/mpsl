using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace mps.Model
{
    /// <summary>
    /// A shopping list item is considered unique in the context of its shopping list.
    /// Features like history, suggestions and statistics are also scoped to the parent list.
    /// </summary>
    [Index(nameof(Name), nameof(ShoppingListId), IsUnique = true)]
    public class ShoppingListItem : EntityBase, ITrackLastChange
    {
        public int ShoppingListId { get; set; }
        
        [ForeignKey(nameof(ShoppingListId))]
        public virtual ShoppingList ShoppingList { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a normalized version of the name that does not contain special chracters
        /// and replaces umlauts with escape sequences.
        /// displayed as "avatar".
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string NormalizedName { get; set; }

        /// <summary>
        /// Gets or sets the unique short name for the item that will be 
        /// displayed as "avatar".
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string ShortName { get; set; }

        /// <summary>
        /// Gets or sets an additional text describing the item in depth.
        /// </summary>
        [MaxLength(100)]
        public string Hint { get; set; }

        /// <summary>
        /// Gets or sets a property indicating how many of the items to get.
        /// </summary>
        public int? Amount { get; set; }

        public int? UnitId { get; set; }

        [ForeignKey(nameof(UnitId))]
        public virtual Unit Unit { get; set; }

        /// <summary>
        /// Gets or sets a property indicating if should show up in the shopping list.
        /// </summary>
        public ItemState Status { get; set; }

        /// <summary>
        /// Gets or sets a property indicating when the item was completed.
        /// </summary>
        /// <remarks>
        /// Items checked more than one hour ago will be moved to the history.
        /// </remarks
        public DateTime? CheckDate { get; set; }

        public string CheckedByUserId { get; set; }

        [ForeignKey(nameof(CheckedByUserId))]
        public virtual ApplicationUser CheckedByUser { get; set; }


        /// <summary>
        /// Gets or sets a property indicating the last time the item was added to the shopping list.
        /// </summary>
        /// <remarks>
        /// Used to sort the items in the order they where added to the shopping list.
        /// </remarks
        public DateTime AddDate { get; set; }

        public string AddedByUserId { get; set; }

        [ForeignKey(nameof(AddedByUserId))]
        public virtual ApplicationUser AddedByUser { get; set; }

        /// <summary>
        /// Gets or sets the user that made the last change to the item.
        /// </summary>
        [ForeignKey(nameof(LastChangeByUserId))]
        public virtual ApplicationUser LastChangeByUser { get; set; }

        /// <summary>
        /// Gets or sets the ID of user that made the last change to the item.
        /// </summary>
        /// <remarks>
        /// Nullable since the field was introduced in a later version. Should not be 
        /// null tough.
        /// </remarks
        public string LastChangeByUserId { get; set; }
        public DateTime? LastChangedDate { get; set; }

        /// <summary>
        /// A complete record of the times the item was wanted on the list.
        /// </summary>
        public virtual ICollection<ItemHistory> History {get; set;}

    }
}