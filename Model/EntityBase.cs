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

        /// <summary>
        /// Gets or sets the user that made the last change to the item.
        /// </summary>
        [ForeignKey(nameof(LastChangeByUserId))]
        public virtual ApplicationUser LastChangeByUser { get; set; }

        /// <summary>
        /// Gets or sets the ID of user that made the last change to the item.
        /// </summary>
        /// <remarks>
        /// Nullable since the field was introduced in a later version. Should not be 
        /// null tough.
        /// </remarks
        public string LastChangeByUserId { get; set; }
    }
}