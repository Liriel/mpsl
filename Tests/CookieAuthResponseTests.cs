using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests;

/// <summary>
/// The other authorization tests run against a header-driven test scheme, so they cannot see how the
/// real Identity application cookie answers a denial. These assert the production cookie configuration
/// directly: the API must return status codes, never a redirect to a login/access-denied page.
/// </summary>
public class CookieAuthResponseTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory factory;

    public CookieAuthResponseTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task UnauthenticatedRequest_Returns401_NotARedirect()
    {
        var context = await InvokeAsync((events, ctx) => events.RedirectToLogin(ctx));

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    /// <summary>
    /// Every server-side access check added for F1 calls <c>Forbid()</c>. Without an explicit
    /// OnRedirectToAccessDenied handler the cookie middleware answers that with a 302 to the (empty)
    /// AccessDeniedPath, so the SPA would receive index.html instead of a 403.
    /// </summary>
    [Fact]
    public async Task ForbiddenRequest_Returns403_NotARedirect()
    {
        var context = await InvokeAsync((events, ctx) => events.RedirectToAccessDenied(ctx));

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    private async Task<HttpContext> InvokeAsync(
        Func<CookieAuthenticationEvents, RedirectContext<CookieAuthenticationOptions>, Task> invoke)
    {
        using var scope = this.factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var scheme = new AuthenticationScheme(
            IdentityConstants.ApplicationScheme,
            IdentityConstants.ApplicationScheme,
            typeof(CookieAuthenticationHandler));

        var redirectContext = new RedirectContext<CookieAuthenticationOptions>(
            httpContext, scheme, options, new AuthenticationProperties(), "/redirect-target");

        await invoke(options.Events, redirectContext);
        return httpContext;
    }
}
