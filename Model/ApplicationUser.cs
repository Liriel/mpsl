using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace mps.Model
{
    public class ApplicationUser : IdentityUser
    {
        public ApplicationUser() { }
        public ApplicationUser(string userName)
        : base(userName)
        { }

        /// <summary>
        /// Gets or sets a short version of the user name
        /// that will be displayed in the UI.
        /// </summary>
        [MaxLength(20)]
        public string ShortName { get; set; }

        public bool ShowShortnameInList { get; set; }
    }
}