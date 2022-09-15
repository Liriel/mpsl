using System.ComponentModel.DataAnnotations;

namespace mps.ViewModels
{
    public class ShoppingListAddViewModel : EditViewModel {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        [MaxLength(5)]
        public string UnitShortName {get; set;}

        public int? Amount { get; set; }
    }
}