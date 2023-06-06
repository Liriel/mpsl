namespace mps.Model
{
    /// <summary>
    /// Use this interface to decorate entity that track the last change user / timestamp.
    /// </summary>
    public interface ITrackLastChange
    {

        /// <summary>
        /// Gets or sets the ID of user that made the last change to the item.
        /// </summary>
        /// <remarks>
        /// Nullable since the field was introduced in a later version. Should not be 
        /// null tough.
        /// </remarks
        string LastChangeByUserId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the entity was changed.
        /// </summary>
        DateTime? LastChangedDate { get; set; }
    }
}