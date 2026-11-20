using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

builder.Services.AddAuthorization(options =>
    options.AddPolicy("ApprovedUser", p => p.RequireRole("user", "admin")));

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

    // same for authenticated-but-not-approved callers: the access checks call Forbid(), which would
    // otherwise redirect to the (empty) AccessDeniedPath and hand the SPA an HTML page instead of a status
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
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
builder.Services.AddScoped<IPushSubscriptionService, PushSubscriptionService>();
builder.Services.AddScoped<IShoppingListAccessService, ShoppingListAccessService>();
builder.Services.AddSingleton<IWebPushSender, WebPushSender>();
builder.Services.AddSingleton<IWebPushNotificationService, WebPushNotificationService>();
builder.Services.AddTransient<IEntityService<ShoppingList>, ShoppingListEntityService>();
builder.Services.AddTransient<IEntityService<Unit>, EntityService<Unit>>();
builder.Services.AddTransient<IEntityService<ShoppingListItem>, ShoppingListItemEntityService>();
builder.Services.AddTransient<IUnitService, UnitService>();
builder.Services.AddTransient<IDeclutterService, DeclutterService>();
builder.Services.AddScoped<IItemArchiveService, ItemArchiveService>();
builder.Services.AddScoped<IItemMatchService, ItemMatchService>();
builder.Services.AddTransient<IItemMergeService, ItemMergeService>();
builder.Services.AddTransient<AppDbInitializer>();
builder.Services.AddSignalR();
builder.Services.AddMvc();
builder.Services.AddAutoMapper(typeof(MyProfile));

builder.Services.AddControllers();

// [ApiController] rejects an invalid ModelState in a filter, before the action runs, and would
// answer with an RFC 7807 ProblemDetails body. Emit the same { success, entity, validationErrors }
// shape the controllers use for every other rejected write, so a client only has to understand one.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
        new BadRequestObjectResult(ValidationFailureResult.FromModelState(context.ModelState));
});

// backs the container HEALTHCHECK in the Dockerfiles
builder.Services.AddHealthChecks();

// reverse proxy support
// https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-5.0
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // HACK: allow RP traffic from anywhere
    options.KnownIPNetworks.Clear();
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

// must stay anonymous - the container healthcheck has no credentials. Kept above the
// SPA fallback so it answers with a status code instead of index.html.
app.MapHealthChecks("/health").AllowAnonymous();

app.MapFallbackToFile("index.html");;

app.Run();

// allows WebApplicationFactory<Program> to reference this entry point assembly from Tests/
public partial class Program { }