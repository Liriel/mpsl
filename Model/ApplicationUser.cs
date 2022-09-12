using Microsoft.AspNetCore.Identity;

namespace mps.Model
{
    public class ApplicationUser : IdentityUser
    {
        public ApplicationUser() { }
        public ApplicationUser(string userName)
        : base(userName)
        { }
    }
}