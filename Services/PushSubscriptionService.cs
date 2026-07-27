using mps.Model;

namespace mps.Services;

public interface IPushSubscriptionService
{
    Task<PushSubscription> SubscribeAsync(string userId, string endpoint, string p256dh, string auth, string userAgent);
    Task<bool> UnsubscribeAsync(string userId, string endpoint);
    Task<IEnumerable<PushSubscription>> GetUserSubscriptionsAsync(string userId);
    Task<IEnumerable<PushSubscription>> GetAllSubscriptionsExceptUserAsync(string excludeUserId);
    Task<IEnumerable<PushSubscription>> GetAllSubscriptionsAsync();
    Task<PushSubscription> GetSubscriptionByIdAsync(int subscriptionId);
}

public class PushSubscriptionService : IPushSubscriptionService
{
    private readonly IRepository repo;
    private readonly ILogger<PushSubscriptionService> logger;

    public PushSubscriptionService(IRepository repo, ILogger<PushSubscriptionService> logger)
    {
        this.repo = repo;
        this.logger = logger;
    }

    public async Task<PushSubscription> SubscribeAsync(string userId, string endpoint, string p256dh, string auth, string userAgent)
    {
        // Check if subscription already exists
        var existing = this.repo.GetEntities<PushSubscription>()
            .FirstOrDefault(s => s.UserId == userId && s.Endpoint == endpoint);

        if (existing != null)
        {
            // Update existing subscription
            existing.P256dh = p256dh;
            existing.Auth = auth;
            existing.UserAgent = userAgent;
            existing.CreatedAt = DateTime.UtcNow;
            this.logger.LogInformation($"Updated push subscription for user {userId}");
        }
        else
        {
            // Create new subscription
            existing = new PushSubscription
            {
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            };
            this.repo.Add(existing);
            this.logger.LogInformation($"Created new push subscription for user {userId}");
        }

        await this.repo.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> UnsubscribeAsync(string userId, string endpoint)
    {
        var subscription = this.repo.GetEntities<PushSubscription>()
            .FirstOrDefault(s => s.UserId == userId && s.Endpoint == endpoint);

        if (subscription != null)
        {
            this.repo.Delete(subscription);
            await this.repo.SaveChangesAsync();
            this.logger.LogInformation($"Removed push subscription for user {userId}");
            return true;
        }

        return false;
    }

    public async Task<IEnumerable<PushSubscription>> GetUserSubscriptionsAsync(string userId)
    {
        return await Task.FromResult(
            this.repo.GetEntities<PushSubscription>()
                .Where(s => s.UserId == userId)
                .ToList()
        );
    }

    public async Task<IEnumerable<PushSubscription>> GetAllSubscriptionsExceptUserAsync(string excludeUserId)
    {
        return await Task.FromResult(
            this.repo.GetEntities<PushSubscription>()
                .Where(s => s.UserId != excludeUserId)
                .ToList()
        );
    }

    public async Task<IEnumerable<PushSubscription>> GetAllSubscriptionsAsync()
    {
        return await Task.FromResult(
            this.repo.GetEntities<PushSubscription>()
                .ToList()
        );
    }

    public async Task<PushSubscription> GetSubscriptionByIdAsync(int subscriptionId)
    {
        return await Task.FromResult(
            this.repo.GetEntities<PushSubscription>()
                .FirstOrDefault(s => s.Id == subscriptionId)
        );
    }
}
