using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mps.Services;
using mps.ViewModels;

namespace mps.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PushSubscriptionController : ControllerBase
{
    private readonly IPushSubscriptionService subscriptionService;
    private readonly IUserIdentityService userIdentityService;
    private readonly ILogger<PushSubscriptionController> logger;
    private readonly IConfiguration configuration;

    public PushSubscriptionController(
        IPushSubscriptionService subscriptionService,
        IUserIdentityService userIdentityService,
        ILogger<PushSubscriptionController> logger,
        IConfiguration configuration)
    {
        this.subscriptionService = subscriptionService;
        this.userIdentityService = userIdentityService;
        this.logger = logger;
        this.configuration = configuration;
    }

    /// <summary>
    /// Get the VAPID public key for push subscription
    /// </summary>
    [HttpGet("vapid-public-key")]
    public IActionResult GetVapidPublicKey()
    {
        var publicKey = this.configuration["VapidKeys:PublicKey"];
        return Ok(new { publicKey });
    }

    /// <summary>
    /// Subscribe to push notifications
    /// </summary>
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = this.userIdentityService.CurrentUserId;
            var userAgent = Request.Headers["User-Agent"].ToString();

            var subscription = await this.subscriptionService.SubscribeAsync(
                userId,
                model.Endpoint,
                model.P256dh,
                model.Auth,
                userAgent
            );

            this.logger.LogInformation($"User {userId} subscribed to push notifications");
            return Ok(new { success = true, subscriptionId = subscription.Id });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error subscribing to push notifications");
            return StatusCode(500, new { error = "Failed to subscribe to push notifications" });
        }
    }

    /// <summary>
    /// Unsubscribe from push notifications
    /// </summary>
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushSubscriptionViewModel model)
    {
        try
        {
            var userId = this.userIdentityService.CurrentUserId;
            var result = await this.subscriptionService.UnsubscribeAsync(userId, model.Endpoint);

            if (result)
            {
                this.logger.LogInformation($"User {userId} unsubscribed from push notifications");
                return Ok(new { success = true });
            }

            return NotFound(new { error = "Subscription not found" });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error unsubscribing from push notifications");
            return StatusCode(500, new { error = "Failed to unsubscribe from push notifications" });
        }
    }

    /// <summary>
    /// Get user's current subscriptions
    /// </summary>
    [HttpGet("my-subscriptions")]
    public async Task<IActionResult> GetMySubscriptions()
    {
        try
        {
            var userId = this.userIdentityService.CurrentUserId;
            var subscriptions = await this.subscriptionService.GetUserSubscriptionsAsync(userId);

            return Ok(subscriptions.Select(s => new
            {
                s.Id,
                s.Endpoint,
                s.CreatedAt,
                s.UserAgent
            }));
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error getting subscriptions");
            return StatusCode(500, new { error = "Failed to get subscriptions" });
        }
    }
}
