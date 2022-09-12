using System.Threading.Tasks;
using mps.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace mps.Services{
    public class IdentitySecurityService : ISecurityService
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly ILogger<IdentitySecurityService> logger;
        private readonly IRepository repo;

        public IdentitySecurityService(UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager, ILogger<IdentitySecurityService> logger, IRepository repo){

            this.userManager = userManager;
            this.signInManager = signInManager;
            this.logger = logger;
            this.repo = repo;
        }

        public async Task ClaimAdminAsync(string userId)
        {
            // this only works if there is only one user registered in the system
            // TODO: find a procedure to re-claim admin rights
            if (this.repo.GetUserCount() == 1)
            {
                // get the currntly logged in user
                var user = await this.userManager.FindByIdAsync(userId);
                if (await this.userManager.IsInRoleAsync(user, "admin"))
                {
                    logger.LogWarning($"Admin user {user.UserName} tried to reclaim admin.");
                    return;
                }

                // add the user to the admin role
                await this.userManager.AddToRoleAsync(user, "admin");
                await this.userManager.AddToRoleAsync(user, "user");

                // re-sign in to activate the new roles
                await this.signInManager.RefreshSignInAsync(user);
            }
        }

        public async Task PromoteAsync(string userId)
        {
            var user = await this.userManager.FindByIdAsync(userId);
            if (await this.userManager.IsInRoleAsync(user, "user"))
            {
                logger.LogWarning($"User {user.Id} already promoted");
                return;
            }

            logger.LogInformation($"Promoting user {user.Id}");
            await this.userManager.AddToRoleAsync(user, "user");

            // refresh to activate the new roles
            await this.signInManager.RefreshSignInAsync(user);
        }
    }
}