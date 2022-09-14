using System.ComponentModel.DataAnnotations;

namespace mps.ViewModels
{
    public class UnitEditViewModel : EditViewModel{

        [Required]
        [MaxLength(5)]
        public string ShortName { get; set; }

        [MaxLength(50)]
        public string Name { get; set; }
    }
}