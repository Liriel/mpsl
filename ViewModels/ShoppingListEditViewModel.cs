using System.ComponentModel.DataAnnotations;

namespace mps.ViewModels
{
    public class ShoppingListEditViewModel : EditViewModel{

        // ShoppingList.Name is [Required] in the model - without the same constraint here a null
        // name passes ModelState and fails at the database as a 500 instead of a 400.
        [Required]
        [MaxLength(50)]
        public string Name { get; set; }
    }
}