using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using mps.Infrastructure;
using mps.Model;
using mps.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<DataContext>();
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddRoleManager<RoleManager<IdentityRole>>()
    .AddDefaultTokenProviders()
    .AddEntityFrameworkStores<DataContext>();

builder.Services.AddAuthentication()
.AddMicrosoftAccount(microsoftOptions =>
{
    microsoftOptions.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
    microsoftOptions.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
    microsoftOptions.AuthorizationEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
});

builder.Services.ConfigureApplicationCookie(options =>
{
    // Cookie settings
    options.Cookie.HttpOnly = true;

    options.LoginPath = string.Empty;
    options.AccessDeniedPath = string.Empty;

    // prevent the default redirect and send a proper 401
    // https://stackoverflow.com/a/59093237/1859022
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
});

builder.Services.AddScoped<IRepository, PersistedRepository>();
builder.Services.AddScoped<IUserIdentityService, HttpContextIdentityService>();
builder.Services.AddTransient<IEntityService<ShoppingList>, ShoppingListEntityService>();
builder.Services.AddTransient<IEntityService<Unit>, EntityService<Unit>>();
builder.Services.AddTransient<AppDbInitializer>();
builder.Services.AddSignalR();
builder.Services.AddMvc();
builder.Services.AddAutoMapper(typeof(MyProfile));

builder.Services.AddControllersWithViews();

// reverse proxy support
// https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-5.0
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // HACK: allow RP traffic from anywhere
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

using (var scope = app.Services.CreateScope()){
    var dbInitializer = scope.ServiceProvider.GetService<AppDbInitializer>();
    dbInitializer.UpdateDb();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=FilteredIndex}/{id?}");

app.MapFallbackToFile("index.html");;

app.Run();
