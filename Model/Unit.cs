using System.ComponentModel.DataAnnotations;

namespace mps.Model
{
    public class Unit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(5)]
        public string ShortName { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; }
    }
}