using System.ComponentModel.DataAnnotations;
using mps.Model;

namespace mps.ViewModels
{
    public class ShoppingListItemEditViewModel : EditViewModel{
        public int ShoppingListId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        [MaxLength(3)]
        public string ShortName { get; set; }

        [MaxLength(100)]
        public string Hint { get; set; }

        [MaxLength(5)]
        public string UnitShortName {get; set;}

        public int? Amount { get; set; }

        /// <summary>Null means "not supplied": the stored kind is left alone.</summary>
        public ItemKind? Kind { get; set; }
    }
}