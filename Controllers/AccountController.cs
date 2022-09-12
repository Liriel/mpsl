using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication;
using System;
using System.Security.Claims;
using System.Linq;
using System.Collections.Generic;
using mps.Infrastructure;
using mps.Model;
using mps.ViewModels;
using mps.Services;

namespace tempmon.Web.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly ILogger<AccountController> logger;
        private readonly IRepository repo;

        public AccountController(UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager, ILogger<AccountController> logger, IRepository repo)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.logger = logger;
            this.repo = repo;
        }

        [HttpGet]
        public async Task<ActionResult<UserInfo>> Index()
        {
            int userCount = this.repo.GetUserCount();
            // get the currntly logged in user
            var user = await this.userManager.GetUserAsync(User);
            UserInfo userInfo;

            if (User.Identity.IsAuthenticated)
            {
                userInfo = new UserInfo
                {
                    IsAuthenticated = true,
                    UserName = user.UserName,
                    Roles = await this.userManager.GetRolesAsync(user)
                };
            }
            else
            {
                userInfo = new UserInfo { IsAuthenticated = false };
            }

            userInfo.CanClaimAdmin = user != null && userCount == 1 && !await this.userManager.IsInRoleAsync(user, "admin");
            return userInfo;
        }

        [Authorize]
        [HttpGet("Claim")]
        public async Task<IActionResult> ClaimAdmin()
        {
            // this only works if there is only one user registered in the system
            // TODO: find a procedure to re-claim admin rights
            if (this.repo.GetUserCount() == 1)
            {
                // get the currntly logged in user
                var user = await this.userManager.GetUserAsync(User);
                if (await this.userManager.IsInRoleAsync(user, "admin"))
                {
                    logger.LogWarning($"Admin user {user.UserName} tried to reclaim admin.");
                    return BadRequest($"Admin user {user.UserName} tried to reclaim admin.");
                }

                // add the user to the admin role
                await this.userManager.AddToRoleAsync(user, "admin");
                await this.userManager.AddToRoleAsync(user, "user");

                // re-sign in to activate the new roles
                await this.signInManager.RefreshSignInAsync(user);
                return Ok();
            }

            return Forbid();
        }

        [Authorize(Roles = "admin")]
        [HttpPut("Promote/{userId}")]
        public async Task<IActionResult> Promote(string userId)
        {
            var user = await this.userManager.FindByIdAsync(userId);
            if (await this.userManager.IsInRoleAsync(user, "user"))
            {
                logger.LogWarning($"User {user.Id} already promoted.");
                return BadRequest($"User {user.Id} already promoted.");
            }

            logger.LogInformation($"Promoting user {user.Id}");

            var result = await this.userManager.AddToRoleAsync(user, "user");
            if (result.Succeeded)
                return Ok();
            else
                return BadRequest(result.Errors);
        }

        [Authorize(Roles = "admin")]
        [HttpPut("Demote/{userId}")]
        public async Task<IActionResult> Demote(string userId)
        {
            var rolesToRemove = new List<string>{"user"};
            var currentUser = await this.userManager.GetUserAsync(this.User);
            if (currentUser.Id == userId)
                return Unauthorized("Can't demote self");

            var user = await this.userManager.FindByIdAsync(userId);
            if (await this.userManager.IsInRoleAsync(user, "user") == false)
            {
                logger.LogWarning($"User {user.Id} is not in the user role.");
                return BadRequest($"User {user.Id} is not in the user role.");
            }

            logger.LogInformation($"Demoting user {user.Id}.");

            if (await this.userManager.IsInRoleAsync(user, "admin"))
                rolesToRemove.Add("admin");

            var result = await this.userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (result.Succeeded)
                return Ok();
            else
                return BadRequest(result.Errors);
        }

        [Authorize(Roles = "admin")]
        [HttpGet("UserList")]
        public ActionResult UserList()
        {
            var userRoleId = this.repo.Roles.Single(r => r.Name == "user").Id;
            var adminRoleId = this.repo.Roles.Single(r => r.Name == "admin").Id;
            var q = from ur in this.repo.UserRoles.AsEnumerable()
                    group ur.RoleId by ur.UserId into g
                    select g;
            var userRoles = q.ToDictionary(g => g.Key, g => g.ToHashSet());

            var r = from u in this.repo.Users
                    select new
                    {
                        u.Id,
                        u.UserName,
                        Shortname = AvatarHelper.GetShortname(u.UserName, 2),
                        IsUser = userRoles.ContainsKey(u.Id) ? userRoles[u.Id].Contains(userRoleId) : false,
                        IsAdmin = userRoles.ContainsKey(u.Id) ? userRoles[u.Id].Contains(adminRoleId) : false,
                    };

            return Json(r);
        }

        [HttpGet("SignIn")]
        public IActionResult SignIn()
        {
            var props = this.signInManager.ConfigureExternalAuthenticationProperties(
                "Microsoft",
                Url.Action("SignInCallback", "Account")
            );

            var result = new ChallengeResult("Microsoft", props);
            return result;
        }

        [Authorize()]
        [HttpGet("SignOut")]
        public new async Task<IActionResult> SignOut()
        {
            await this.signInManager.SignOutAsync();
            return Redirect("~/");
        }

        [HttpGet("SignInCallback")]
        public async Task<ActionResult<string>> SignInCallback()
        {
            // check if the user already authenticated to the external login
            var info = await this.signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                throw new ApplicationException(
                    "Error loading external login data during confirmation.");
            }

            this.logger.LogInformation("checking external login");
            // try to sign the user in using the external login
            var result = await this.signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: true,
                bypassTwoFactor: true);

            // we are done if the user is already locked out
            if (result.IsLockedOut)
                return "Lockout";

            ApplicationUser user = null;
            if (result.Succeeded)
            {
                this.logger.LogInformation("ExternalLoginSignIn succeeded - signing in");

                // the user already has an account
                // find the user account using the external login key
                user = await this.userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            }
            else
            {
                this.logger.LogInformation("ExternalLoginSignIn not succeeded - creating user");

                // get the email
                string email = info.Principal.FindFirstValue(ClaimTypes.Email);
                string name = info.Principal.FindFirstValue(ClaimTypes.Name);

                // create a new user if there is no account
                user = new ApplicationUser { UserName = email, Email = email };
                this.logger.LogInformation($"UserName: {user.UserName}");
                await this.userManager.CreateAsync(user);
                var addLoginResult = await this.userManager.AddLoginAsync(user, info);
            }

            // Include the access token in the properties
            var props = new AuthenticationProperties();
            props.StoreTokens(info.AuthenticationTokens);
            props.IsPersistent = true;

            await this.signInManager.SignInAsync(user, props, info.LoginProvider);
            return Redirect("~/");
        }
    }
}