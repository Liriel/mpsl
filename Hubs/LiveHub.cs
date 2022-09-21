using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace mps.Hubs
{
    // [Authorize]
    public class LiveHub : Hub
    {
        private readonly ILogger<LiveHub> logger;

        public LiveHub(ILogger<LiveHub> logger)
        {
            this.logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            this.logger.LogInformation($"added connection {this.Context.ConnectionId}");
            await base.OnConnectedAsync();
        }
    }
}