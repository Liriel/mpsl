using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace mps.Model
{
    // can't use a BaseEntity like a normal person because sqlite ef migration
    // does not look for DataAnnotations on base classes
    // TODO: change to EntityBase class for mssql
    public class EntityBase
    {

        [Key]
        public int Id { get; set; }

    }
}