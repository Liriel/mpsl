using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace mps.Model
{
    public class ShoppingList : EntityBase
    {
        [MaxLength(255)]
        [Required]
        public string Name { get; set; }

        [ForeignKey(nameof(OwnerUserKey))]
        public virtual ApplicationUser OwnerUser { get; set; }

        public string OwnerUserKey { get; set; }

        public virtual ICollection<ShoppingListItem> Items { get; set; }
    }
}