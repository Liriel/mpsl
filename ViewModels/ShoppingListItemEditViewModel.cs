using System.ComponentModel.DataAnnotations;

namespace mps.ViewModels
{
    public class ShoppingListItemEditViewModel : EditViewModel{
        public int ShoppingListId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        [Required]
        [MaxLength(3)]
        public string ShortName { get; set; }

        [MaxLength(100)]
        public string Hint { get; set; }

        [MaxLength(5)]
        public string UnitShortName {get; set;}

        public int? Amount { get; set; }
    }
}