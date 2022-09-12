using System.Collections.Generic;

namespace mps.ViewModels
{
    public class UserInfo
    {
        public string UserName { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool CanClaimAdmin { get; set; }
        public IList<string> Roles { get; set; }
    }
}