namespace mps.Services
{
    public interface IUserIdentityService
    {
        /// <summary>
        /// Gets the username of the user currently signed in.
        /// </summary>
        string CurrentUserName { get; }

        /// <summary>
        /// Gets the id of the user currently signed in.
        /// </summary>
        string CurrentUserId { get; }
    }
}