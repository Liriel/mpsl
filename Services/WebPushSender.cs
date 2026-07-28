using WebPush;

namespace mps.Services;

/// <summary>
/// Thin seam around <see cref="WebPushClient"/> so tests can substitute a fake that never hits the
/// network, while still being able to throw <see cref="WebPushException"/> to exercise the
/// subscription-pruning logic in <see cref="WebPushNotificationService"/>.
/// </summary>
public interface IWebPushSender
{
    Task SendAsync(PushSubscription subscription, string payloadJson, VapidDetails vapidDetails);
}

public class WebPushSender : IWebPushSender
{
    private readonly WebPushClient webPushClient = new();

    public Task SendAsync(PushSubscription subscription, string payloadJson, VapidDetails vapidDetails)
    {
        return this.webPushClient.SendNotificationAsync(subscription, payloadJson, vapidDetails);
    }
}
