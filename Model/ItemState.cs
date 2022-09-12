namespace mps.Model
{
    /// <summary>
    ///  Indicates the shopping list item state.
    /// </summary>
    public enum ItemState {
        
        /// <summary>
        /// The item is on the list and yet to collect.
        /// </summary>
        Open = 1,

        /// <summary>
        /// The item is on the list and has been marked as done / collected / bought.
        /// </summary>
        Checked = 2,

        /// <summary>
        /// The last time the item was collected has been recorded in the item history
        /// and it is currently not on the list.
        /// </summary>
        Archived = 3
    }
}