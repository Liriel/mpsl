using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace mps.Model
{
    [Index(nameof(ShortName), IsUnique = true)]
    public class Unit : IEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(5)]
        public string ShortName { get; set; }

        [MaxLength(50)]
        public string Name { get; set; }
    }
}