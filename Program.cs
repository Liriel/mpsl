using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using mps.Hubs;
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
    microsoftOptions.TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
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

builder.Services.AddCors(o => o.AddPolicy("CorsPolicy", builder =>
{
    builder
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
        .WithOrigins("https://localhost:44443", "https://192.168.0.96:44443");
        // .WithOrigins("https://localhost:44443");
}));

builder.Services.AddScoped<IRepository, PersistedRepository>();
builder.Services.AddScoped<IUserIdentityService, HttpContextIdentityService>();
builder.Services.AddScoped<INotificationService, SignalRNotificationService>();
builder.Services.AddTransient<IEntityService<ShoppingList>, ShoppingListEntityService>();
builder.Services.AddTransient<IEntityService<Unit>, EntityService<Unit>>();
builder.Services.AddTransient<IEntityService<ShoppingListItem>, ShoppingListItemEntityService>();
builder.Services.AddTransient<IUnitService, UnitService>();
builder.Services.AddTransient<AppDbInitializer>();
builder.Services.AddSignalR();
builder.Services.AddMvc();
builder.Services.AddAutoMapper(typeof(MyProfile));

builder.Services.AddControllers();

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

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseCors("CorsPolicy");

using (var scope = app.Services.CreateScope()){
    var dbInitializer = scope.ServiceProvider.GetService<AppDbInitializer>();
    dbInitializer.UpdateDb();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=FilteredIndex}/{id?}");

app.MapHub<LiveHub>("/live");

app.MapFallbackToFile("index.html");;

app.Run();