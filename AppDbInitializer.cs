using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using mps.Model;

public class AppDbInitializer
{
    //private readonly RoleManager<IdentityRole> roleManager;
    private readonly ILogger<AppDbInitializer> logger;
    private readonly DataContext context;
    private readonly RoleManager<IdentityRole> roleManager;

    public AppDbInitializer(ILogger<AppDbInitializer> logger, DataContext context, RoleManager<IdentityRole> roleManager)
    {
        this.logger = logger;
        this.context = context;
        this.roleManager = roleManager;
    }

    // migrate to the latest version of the database and create the default roles
    // if they do not exist
    public async Task UpdateDbAsync()
    {
        var defaultRoles = new string[] { "admin", "user" };
        context.Database.Migrate();

        foreach (string role in defaultRoles)
        {
            if (await this.roleManager.RoleExistsAsync(role) == false)
                await this.roleManager.CreateAsync(new IdentityRole(role));
        }

    }

    public void UpdateDb()
    {
        this.UpdateDbAsync().Wait();
    }
}