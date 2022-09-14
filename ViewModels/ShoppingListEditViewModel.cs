using System.ComponentModel.DataAnnotations;

namespace mps.ViewModels
{
    public class ShoppingListEditViewModel : EditViewModel{

        [MaxLength(50)]
        public string Name { get; set; }
    }
}