using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using mps.Model;

namespace Tests;

/// <summary>
/// Boots the real app (migrations, role seeding, controllers, hub - everything wired up in
/// Program.cs) against a throw-away temp SQLite file instead of data/mps.db, with
/// <see cref="TestAuthHandler"/> as the default authentication scheme. This never touches
/// the real database.
/// </summary>
public class TestAppFactory : WebApplicationFactory<Program>
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"mps-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // keeps Microsoft.AspNetCore.SpaProxy dormant - it only activates in Development.
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<DataContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<DataContext>(options =>
            {
                options.UseLazyLoadingProxies();
                options.UseSqlite($"Data Source={this.dbPath}");
            });

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // AuthenticationMiddleware probes every registered "request handler" (remote/OAuth)
            // scheme on each request regardless of the default scheme, which would otherwise
            // throw here because no real Microsoft ClientId/ClientSecret is configured for tests.
            // PostConfigure always runs last, after Program.cs's own Configure delegate.
            services.PostConfigure<MicrosoftAccountOptions>(MicrosoftAccountDefaults.AuthenticationScheme, o =>
            {
                o.ClientId = "test-client-id";
                o.ClientSecret = "test-client-secret";
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(this.dbPath))
        {
            try
            {
                File.Delete(this.dbPath);
            }
            catch (IOException)
            {
                // best-effort cleanup of the temp file
            }
        }
    }
}
