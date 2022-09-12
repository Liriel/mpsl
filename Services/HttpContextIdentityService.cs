using System.Linq;
using Microsoft.AspNetCore.Http;

namespace mps.Services
{
    public class HttpContextIdentityService : IUserIdentityService
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        public HttpContextIdentityService(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        // can't use Environment.UserName here because that is the application pool identity (could enable impersonation though)
        public string CurrentUserName => this.httpContextAccessor.HttpContext?.User?.Identity?.Name;

        public string CurrentUserId => this.httpContextAccessor.HttpContext?.User?.Claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}