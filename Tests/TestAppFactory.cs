using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using mps.Model;
using mps.Services;
using WebPush;
using WebPushSubscription = WebPush.PushSubscription;

namespace Tests;

/// <summary>
/// Stand-in for <see cref="IWebPushSender"/> so the throttled sends
/// <see cref="mps.Services.WebPushNotificationService"/> schedules from a background
/// <c>Task.Run</c> never attempt a real network call during a test run. Also records every call
/// (thread-safe - the sends happen off the test's own thread) so tests can assert what was sent to
/// whom without a real push service on the other end.
/// </summary>
internal class RecordingWebPushSender : IWebPushSender
{
    private readonly ConcurrentQueue<(string Endpoint, string PayloadJson)> calls = new();

    public IReadOnlyCollection<(string Endpoint, string PayloadJson)> Calls => this.calls.ToArray();

    public Task SendAsync(WebPushSubscription subscription, string payloadJson, VapidDetails vapidDetails)
    {
        this.calls.Enqueue((subscription.Endpoint, payloadJson));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Boots the real app (migrations, role seeding, controllers, hub - everything wired up in
/// Program.cs) against a throw-away temp SQLite file instead of data/mps.db, with
/// <see cref="TestAuthHandler"/> as the default authentication scheme. This never touches
/// the real database.
/// </summary>
public class TestAppFactory : WebApplicationFactory<Program>
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"mps-test-{Guid.NewGuid():N}.db");

    // xUnit's IClassFixture<TestAppFactory> activation requires the fixture type to expose exactly one
    // public constructor, so per-test overrides can't be constructor arguments (AuthorizationTests,
    // CookieAuthResponseTests and LiveHubTests all rely on the implicit parameterless constructor).
    // These are settable properties instead: ConfigureWebHost only runs lazily, the first time the host
    // is actually built (first access to Server/Services/CreateClient()), so setting them right after
    // `new TestAppFactory()` and before touching any of those is enough.
    //
    /// <summary>
    /// Overrides <c>PushNotifications:CollectWindowSeconds</c> to a millisecond-scale value so
    /// push-throttle tests don't wait out the real 10s production default. Null (the default) leaves
    /// production configuration untouched.
    /// </summary>
    public double? PushCollectWindowSeconds { get; set; }

    /// <summary>
    /// Overrides <c>PushNotifications:CooldownMinutes</c> to a millisecond-scale value (expressed in
    /// minutes) so push-throttle tests don't wait out the real 5 minute production default. Null (the
    /// default) leaves production configuration untouched.
    /// </summary>
    public double? PushCooldownMinutes { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // keeps Microsoft.AspNetCore.SpaProxy dormant - it only activates in Development.
        builder.UseEnvironment("Testing");

        if (this.PushCollectWindowSeconds.HasValue || this.PushCooldownMinutes.HasValue)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var overrides = new Dictionary<string, string?>();
                if (this.PushCollectWindowSeconds.HasValue)
                    overrides["PushNotifications:CollectWindowSeconds"] = this.PushCollectWindowSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (this.PushCooldownMinutes.HasValue)
                    overrides["PushNotifications:CooldownMinutes"] = this.PushCooldownMinutes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

                config.AddInMemoryCollection(overrides);
            });
        }

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

            // WebPushNotificationService schedules sends on a background Task.Run - swap the real
            // sender out for a recording fake so a test run never fires a real push over the network,
            // while still letting tests assert what would have been sent to whom.
            var webPushSenderDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IWebPushSender));
            if (webPushSenderDescriptor != null)
                services.Remove(webPushSenderDescriptor);

            services.AddSingleton<IWebPushSender, RecordingWebPushSender>();

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
