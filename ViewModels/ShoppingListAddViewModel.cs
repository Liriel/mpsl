using System.ComponentModel.DataAnnotations;
using mps.Model;

namespace mps.ViewModels
{
    public class ShoppingListAddViewModel : EditViewModel {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        [MaxLength(5)]
        public string UnitShortName {get; set;}

        public int? Amount { get; set; }

        /// <summary>Explicit kind; null lets the server decide (a "!" prefix, else the classifier).</summary>
        public ItemKind? Kind { get; set; }

        /// <summary>Response only: the server picked <see cref="Kind"/> with the classifier.</summary>
        public bool KindAuto { get; set; }

        /// <summary>Response only: why the classifier chose <see cref="Kind"/>.</summary>
        public string KindReason { get; set; }
    }
}