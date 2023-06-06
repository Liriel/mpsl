using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace mps.Model
{
    [Index(nameof(ShortName), IsUnique = true)]
    public class Unit : EntityBase
    {
        [Required]
        [MaxLength(5)]
        public string ShortName { get; set; }

        [MaxLength(50)]
        public string Name { get; set; }
    }
}